// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace idunno.AtProto.Json
{
//    // Adjusted from https://github.com/dotnet/runtime/issues/72604 while we wait for .NET 8 to EOL.
//    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
//    public sealed class PolymorphicJsonDerivedTypeAttribute : Attribute
//    {
//        public PolymorphicJsonDerivedTypeAttribute(Type derivedType, string typeDiscriminator)
//        {
//            DerivedType = derivedType;
//            TypeDiscriminator = typeDiscriminator;
//        }

//        public Type DerivedType { get; }

//        public string TypeDiscriminator { get; }
//    }

//    public sealed class PolymorphicJsonConverterAttribute : JsonConverterAttribute
//    {
//        public PolymorphicJsonConverterAttribute(string discriminatorPropertyName = "$type")
//        {
//            ArgumentNullException.ThrowIfNullOrEmpty(discriminatorPropertyName, nameof(discriminatorPropertyName));

//            DiscriminatorPropertyName = discriminatorPropertyName;
//        }

//        public string DiscriminatorPropertyName { get; }

//        public Type? DefaultType { get; set; }

//        public Type? UndefinedType { get; set; }

//        public Type? UnknownType { get; set; }
//}

//    public sealed class PolymorphicJsonConverterFactory : JsonConverterFactory
//    {
//        public override bool CanConvert(Type typeToConvert)
//        {
//            ArgumentNullException.ThrowIfNull(typeToConvert);

//            return typeToConvert.IsAbstract && typeToConvert.GetCustomAttributes<PolymorphicJsonDerivedTypeAttribute>().Any();
//        }

//        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
//        {
//            return (JsonConverter?)Activator.CreateInstance(
//                typeof(PolymorphicJsonConverter<>).MakeGenericType(typeToConvert), options);
//        }
//    }

//    public sealed class PolymorphicJsonConverter<T> : JsonConverter<T>
//    {
//        private readonly string _discriminatorPropertyName;
//        private readonly Dictionary<string, Type> _discriminatorToSubtype = new();

//        public PolymorphicJsonConverter() : this(new JsonSerializerOptions())
//        {

//        }

//        public PolymorphicJsonConverter(JsonSerializerOptions options)
//        {
//            ArgumentNullException.ThrowIfNull(options);

//            _discriminatorPropertyName = options.PropertyNamingPolicy?.ConvertName("$type") ?? "$type";

//            foreach (PolymorphicJsonDerivedTypeAttribute subtype in typeof(T).GetCustomAttributes<PolymorphicJsonDerivedTypeAttribute>())
//            {
//                _discriminatorToSubtype.Add(subtype.TypeDiscriminator, subtype.DerivedType);
//            }
//        }

//        public override bool CanConvert(Type typeToConvert) => typeof(T) == typeToConvert;

//        public override T Read(
//            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//        {
//            Utf8JsonReader reader2 = reader;

//            using JsonDocument doc = JsonDocument.ParseValue(ref reader2);
//            JsonElement root = doc.RootElement;
//            JsonElement typeField = root.GetProperty(_discriminatorPropertyName);

//            if (typeField.GetString() is not { } typeName)
//            {
//                throw new JsonException(
//                    $"Could not find string property {_discriminatorPropertyName} " +
//                    $"when trying to deserialize {typeof(T).Name}");
//            }

//            if (!_discriminatorToSubtype.TryGetValue(typeName, out Type? type))
//            {
//                throw new JsonException($"Unknown type: {typeName}");
//            }

//            return (T)JsonSerializer.Deserialize(ref reader, type, options)!;
//        }

//        public override void Write(
//            Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
//        {
//            Type type = value!.GetType();
//            JsonSerializer.Serialize(writer, value, type, options);
//        }
//    }
}
