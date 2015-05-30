namespace HyperTomlProcessor
{
    internal enum TomlItemType
    {
        None,
        BasicString,
        MultilineBasicString,
        LiteralString,
        MultilineLiteralString,
        Integer,
        Float,
        Boolean,
        Datetime,
        Array,
        Table,
        InlineTable
    }

    internal static class TomlItemTypeExtensions
    {
        internal static TomlItemType Normalize(this TomlItemType source)
        {
            switch (source)
            {
                case TomlItemType.MultilineBasicString:
                case TomlItemType.LiteralString:
                case TomlItemType.MultilineLiteralString:
                    return TomlItemType.BasicString;
            }
            return source;
        }
    }
}
