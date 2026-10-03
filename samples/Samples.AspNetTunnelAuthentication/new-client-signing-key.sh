#!/usr/bin/env bash
set -euo pipefail
umask 077

usage() {
    printf 'Usage: %s [--output-directory DIRECTORY] [--new-key | --force]\n' "$0"
}

output_directory="${HOME:?HOME must be set}/.blueskyDotnet"
new_key=false
force=false
while (($#)); do
    case "$1" in
        --output-directory)
            if (($# < 2)); then usage >&2; exit 1; fi
            output_directory="$2"
            shift 2
            ;;
        --new-key) new_key=true; shift ;;
        --force) force=true; shift ;;
        --help|-h) usage; exit 0 ;;
        *) usage >&2; exit 1 ;;
    esac
done

if $new_key && $force; then
    printf 'Use either --new-key or --force, not both.\n' >&2
    exit 1
fi
command -v openssl >/dev/null
command -v python3 >/dev/null

mkdir -p -- "$output_directory"
output_directory="$(cd -- "$output_directory" && pwd -P)"
key_name=client-signing-key
if $new_key; then
    key_name="client-signing-key-$(openssl rand -hex 16)"
fi
private_key="$output_directory/$key_name.pem"
public_key="$output_directory/$key_name.jwk.json"

if [[ -L "$private_key" || -L "$public_key" ]]; then
    printf 'Refusing to write through a symbolic link.\n' >&2
    exit 1
fi
if ! $force && [[ -e "$private_key" || -e "$public_key" ]]; then
    printf 'A signing key already exists. Use --new-key to create another, or --force to replace it.\n' >&2
    exit 1
fi

# Reserve the file without clobbering it, then restrict access before writing key material.
if [[ ! -e "$private_key" ]]; then
    (set -o noclobber; : > "$private_key")
fi
chmod 600 -- "$private_key"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out "$private_key"

python3 - "$private_key" "$public_key" "$HOME" <<'PY'
import base64
import hashlib
import json
import pathlib
import subprocess
import sys

private_key, public_key, home = map(pathlib.Path, sys.argv[1:])
der = subprocess.run(
    ["openssl", "pkey", "-in", str(private_key), "-pubout", "-outform", "DER"],
    check=True, capture_output=True,
).stdout
# SubjectPublicKeyInfo for id-ecPublicKey/prime256v1 with an uncompressed P-256 point.
prefix = bytes.fromhex("3059301306072a8648ce3d020106082a8648ce3d03010703420004")
if len(der) != len(prefix) + 64 or not der.startswith(prefix):
    raise ValueError("OpenSSL returned an unexpected P-256 public key encoding")

def encode(value):
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode("ascii")

x, y = encode(der[-64:-32]), encode(der[-32:])
canonical = json.dumps({"crv": "P-256", "kty": "EC", "x": x, "y": y},
                       separators=(",", ":"), sort_keys=True).encode("ascii")
kid = encode(hashlib.sha256(canonical).digest())
jwk = {"kty": "EC", "crv": "P-256", "x": x, "y": y, "kid": kid, "alg": "ES256", "use": "sig"}
public_key.write_text(json.dumps(jwk, indent=2) + "\n", encoding="utf-8")

def configuration_path(path):
    try:
        return "~/" + path.relative_to(home.resolve()).as_posix()
    except ValueError:
        return str(path)

additional = sorted(path for path in private_key.parent.glob("client-signing-key*.pem")
                    if path.is_file() and path != private_key)
print(f"Private key: {private_key}\nPublic JWK:  {public_key}\nKey ID:      {kid}")
print("\nMerge these settings into BlueskyAgent:OAuthOptions in appsettings.json:")
print(json.dumps({
    "ClientSigningKeyPath": configuration_path(private_key),
    "AdditionalClientSigningKeyPaths": [configuration_path(path) for path in additional],
}, indent=2))
print("\nAdditional paths include other client-signing-key*.pem files in this directory. Review the list before using it.")
print("Keep ClientSigningKeyId unset to use the thumbprint shown above.")
if additional:
    print("Before promotion, publish the new key as an additional key while keeping the current active key.")
    print("Then use the settings above to promote it, retaining previous keys until their sessions expire.")
print("Restart after each configuration change, or redeploy if configuration is packaged with the application.")
PY
