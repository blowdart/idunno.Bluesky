// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// A set of data describing the <see cref="Did"/> subject.
    /// </summary>
    /// <remarks>
    /// <para>See https://www.w3.org/TR/did-core/#dfn-did-documents for details.</para>
    /// </remarks>
    public sealed record DidDocument
    {
        /// <summary>
        /// Creates a new instance of <see cref="DidDocument"/>.
        /// </summary>
        /// <param name="id">The <see cref="Did"/> of the subject for the <see cref="DidDocument"/>.</param>
        /// <param name="context">The context of for the <see cref="DidDocument"/>.</param>
        /// <param name="alsoKnownAs">One or more alternative identifiers for the subject of the <see cref="DidDocument"/>.</param>
        /// <param name="verificationMethods">A list of verification methods for the <paramref name="id">subject</paramref> or associated parties.</param>
        /// <param name="services">Ways of communicating with the <paramref name="id">subject</paramref> or associated entities</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
        [JsonConstructor]
        internal DidDocument(
            Did id,
            IReadOnlyList<string>? context,
            IReadOnlyList<string>? alsoKnownAs,
            IReadOnlyList<VerificationMethod>? verificationMethods,
            IReadOnlyList<DidDocService>? services)
        {
            ArgumentNullException.ThrowIfNull(id);

            Id = id;

            if (context is not null)
            {
                Context = context;
            }

            if (alsoKnownAs is not null)
            {
                AlsoKnownAs = alsoKnownAs;
            }

            if (verificationMethods is not null)
            {
                VerificationMethods = verificationMethods;
            }

            if (services is not null)
            {
                Services = services;
            }
        }

        /// <summary>
        /// The context of the current <see cref="DidDocument"/>
        /// </summary>
        /// <remarks>
        /// <para>A context is used to map terms to IRIs. Terms are case sensitive and most valid strings that are not reserved JSON-LD keywords can be used as a term.</para>
        /// </remarks>
        [JsonInclude]
        [JsonPropertyName("@context")]
        public IReadOnlyList<string> Context { get; internal set; } = new List<string>();

        /// <summary>
        /// Gets the <see cref="Did"/> for the subject of the <see cref="DidDocument"/>.
        /// </summary>
        /// <remarks>
        /// <para>See https://www.w3.org/TR/did-core/#did-subject for details.</para>
        /// </remarks>
        [JsonInclude]
        [JsonRequired]
        public Did Id { get; init; }

        /// <summary>
        /// Gets any alternative identifiers for the subject of this <see cref="DidDocument"/>.
        /// </summary>
        /// <remarks>
        /// <para>See https://www.w3.org/TR/did-core/#also-known-as for details.</para>
        /// </remarks>
        [JsonInclude]
        public IReadOnlyList<string>? AlsoKnownAs { get; init; } = new List<string>();

        /// <summary>
        /// Gets verification methods, such as cryptographic public keys, which can be used to authenticate or authorize
        /// interactions with the <see cref="Did"/> subject or associated parties.
        /// </summary>
        ///<remarks>
        /// <para>See https://www.w3.org/TR/did-core/#verification-methods for details.</para>
        /// </remarks>
        [JsonInclude]
        [JsonPropertyName("verificationMethod")]
        public IReadOnlyList<VerificationMethod>? VerificationMethods { get; init; } = new List<VerificationMethod>();

        /// <summary>
        /// Gets ways of communicating with the <see cref="Did"/> subject or associated entities
        /// </summary>
        /// <remarks>
        /// <para>See https://www.w3.org/TR/did-core/#services for details.</para>
        /// </remarks>
        [JsonInclude]
        [JsonPropertyName("service")]
        public IReadOnlyList<DidDocService>? Services { get; init; } = new List<DidDocService>();
    }

    /// <summary>
    /// A set of parameters that can be used together with a process to independently verify a proof.
    /// For example, a cryptographic public key can be used as a verification method with respect to a
    /// digital signature; in such usage, it verifies that the signer possessed the associated
    /// cryptographic private key.
    /// </summary>
    ///<remarks>
    /// <para>See https://www.w3.org/TR/did-core/#verification-methods for details.</para>
    /// </remarks>
    public record VerificationMethod
    {
        /// <summary>
        /// Creates a new instance of <see cref="VerificationMethod"/> with the specified parameters.
        /// </summary>
        /// <param name="id">The <see cref="Did"/> for the <see cref="VerificationMethod"/>.</param>
        /// <param name="type">The type of the <see cref="VerificationMethod"/>.</param>
        /// <param name="controller">The <see cref="Did"/> of the controller for the <see cref="VerificationMethod"/>.</param>
        [JsonConstructor]
        internal VerificationMethod(Uri id, string type, Did controller)
        {
            Id = id;
            Type = type;
            Controller = controller;
        }

        /// <summary>
        /// Gets the ID for this <see cref="VerificationMethod"/>.
        /// </summary>
        public Uri Id { get; init; }

        /// <summary>
        /// Gets the type of the <see cref="VerificationMethod"/>.
        /// </summary>
        public string Type { get; init; }

        /// <summary>
        /// Gets the <see cref="Did"/> for the controller of this <see cref="VerificationMethod"/>.
        /// </summary>
        public Did Controller { get; init; }

        /// <summary>
        /// Gets an optional string representation of a multibase encoded public key for this <see cref="VerificationMethod"/>.
        /// </summary>
        public string? PublicKeyMultibase { get; init; }

        /// <summary>
        /// Gets an optional dictionary of additional properties for this <see cref="VerificationMethod"/>.
        /// </summary>
        /// <value>
        /// An optional dictionary of additional properties for this <see cref="VerificationMethod"/>.
        /// </value>
        [JsonExtensionData]
        public IDictionary<string, object>? Properties { get; }
    }

    /// <summary>
    /// A record containing ways of communicating with the DID subject or associated entities.
    /// </summary>
    /// <remarks>
    /// <para>See https://www.w3.org/TR/did-core/#services for details.</para>
    /// </remarks>
    public record DidDocService
    {
        [JsonConstructor]
        internal DidDocService(string id, string type, Uri serviceEndpoint)
        {
            Id = id;
            Type = type;
            ServiceEndpoint = serviceEndpoint;
        }

        /// <summary>
        /// Gets the Id for this service.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets or the service type.
        /// </summary>
        public string Type { get; init; }

        /// <summary>
        /// Gets the endpoint for this service.
        /// </summary>
        public Uri ServiceEndpoint { get; init; }
    }
}
