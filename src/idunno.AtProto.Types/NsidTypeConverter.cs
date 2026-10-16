// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Globalization;

namespace idunno.AtProto;

internal sealed class NsidTypeConverter : TypeConverter
{
    public NsidTypeConverter()
    {
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string nsid ? new Nsid(nsid) : base.ConvertFrom(context, culture, value);
}
