// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// A set of data describing the <see cref="DID"/> subject.
    /// </summary>
    /// <remarks>
    /// See https://www.w3.org/TR/did-core/#dfn-did-documents for details.
    /// </remarks>
    public sealed record DidDocument
    {
        /// <summary>
        /// Creates a new instance of <see cref="DidDocument"/> for the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The <see cref="Did"/> of the document subject.</param>
        public DidDocument(Did id)
        {
            Id = id;
        }

        [JsonInclude]
        [JsonPropertyName("@context")]
        public IReadOnlyList<string> Context { get; internal set; } = new List<string>();

        /// <summary>
        /// Gets or sets the <see cref="DID"/> for the subject of the <see cref="DidDocument"/>.
        /// </summary>
        /// <value>
        /// The <see cref="DID"/> for the subject of the <see cref="DidDocument"/>.
        /// </value>
        /// <remarks>
        ///See https://www.w3.org/TR/did-core/#did-subject for details.
        /// </remarks>
        public Did Id { get; set; }

        /// <summary>
        /// Gets or sets one or more alternative identifiers for the subject of this <see cref="DidDocument"/>.
        /// </summary>
        /// <value>
        /// A list of alternative identifiers for the subject of this <see cref="DidDocument"/>
        /// </value>
        /// <remarks>
        ///See https://www.w3.org/TR/did-core/#also-known-as for details.
        /// </remarks>
        [JsonInclude]
        public IReadOnlyList<string>? AlsoKnownAs { get; internal set; } = new List<string>();

        /// <summary>
        /// Gets or sets verification methods, such as cryptographic public keys, which can be used to authenticate or authorize
        /// interactions with the <see cref="Did"/> subject or associated parties.
        /// </summary>
        /// <values>
        /// A list of verification methods for the <see cref="Did"/> subject or associated parties.
        /// </values>
        ///<remarks>
        /// See https://www.w3.org/TR/did-core/#verification-methods for details.
        /// </remarks>
        [JsonInclude]
        [JsonPropertyName("verificationMethod")]
        public IReadOnlyList<VerificationMethod>? VerificationMethods { get; internal set; } = new List<VerificationMethod>();

        /// <summary>
        /// Gets or sets ways of communicating with the <see cref="Did"/> subject or associated entities
        /// </summary>
        /// <value>
        /// A list of <see cref="Service"/>s which express of communicating with the <see cref="Did"/> subject or associated entities
        /// </value>
        /// <remarks>
        /// See https://www.w3.org/TR/did-core/#services for details.
        /// </remarks>
        [JsonInclude]
        [JsonPropertyName("service")]
        public IReadOnlyList<Service>? Services { get; internal set; } = new List<Service>();
    }

    /// <summary>
    /// A set of parameters that can be used together with a process to independently verify a proof.
    /// For example, a cryptographic public key can be used as a verification method with respect to a
    /// digital signature; in such usage, it verifies that the signer possessed the associated
    /// cryptographic private key.
    /// </summary>
    ///<remarks>
    /// See https://www.w3.org/TR/did-core/#verification-methods for details.
    /// </remarks>
    public record VerificationMethod
    {
        /// <summary>
        /// Creates a new instance of <see cref="VerificationMethod"/> with the specified parameters.
        /// </summary>
        /// <param name="id">The <see cref="Did"/ for the <see cref="VerificationMethod"/>.</param>
        /// <param name="type">The type of the <see cref="VerificationMethod"/>.</param>
        /// <param name="controller">The <see cref="Did"/ of the controller for the <see cref="VerificationMethod"/>.</param>
        [JsonConstructor]
        public VerificationMethod(Uri id, string type, Did controller)
        {
            Id = id;
            Type = type;
            Controller = controller;
        }

        /// <summary>
        /// Gets or sets the ID for this <see cref="VerificationMethod"/>.
        /// </summary>
        /// <value>
        /// The ID for this <see cref="VerificationMethod"/>.
        /// </value>
        public Uri Id { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="VerificationMethod"/>.
        /// </summary>
        /// <remarks>
        ///The type of the <see cref="VerificationMethod"/>.
        /// </remarks>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Did"/> for the controller of this <see cref="VerificationMethod"/>.
        /// </summary>
        /// <value>
        /// The <see cref="Did"/> for the controller of this <see cref="VerificationMethod"/>.
        /// </value>
        public Did Controller { get; set; }

        /// <summary>
        /// Gets or sets an optional string representation of a multibase encoded public key for this <see cref="VerificationMethod"/>.
        /// </summary>
        /// <value>
        /// An optional string representation of a multibase encoded public key for this <see cref="VerificationMethod"/>.
        /// </value>
        public string? PublicKeyMultibase { get; set; }

        /// <summary>
        /// Gets or sets an optional dictionary of additional properties for this <see cref="VerificationMethod"/>.
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
    /// See https://www.w3.org/TR/did-core/#services for details.
    /// </remarks>
    public record Service
    {
        public Service(string id, string type, Uri serviceEndpoint)
        {
            Id = id;
            Type = type;
            ServiceEndpoint = serviceEndpoint;
        }

        /// <summary>
        /// Gets or sets the Id for this service.
        /// </summary>
        /// <value>The Id for this service.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the service type.
        /// </summary>
        /// <value>The service type.</value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the endpoint for this service.
        /// </summary>
        /// <value>
        /// The endpoint for this service.
        /// </value>
        public Uri ServiceEndpoint { get; set; }
    }
}
