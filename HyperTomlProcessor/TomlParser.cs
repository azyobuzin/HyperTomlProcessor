using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Parseq;
using Parseq.Combinators;

namespace HyperTomlProcessor
{
    internal static class TomlParser
    {
        private static string Unfold(this IEnumerable<IEnumerable<char>> source)
        {
            return string.Concat(source.SelectMany(_ => _));
        }

        internal class TableInfo
        {
            public readonly string[] Name;
            public readonly Comment Comment;
            public readonly bool IsArrayOfTable;

            public TableInfo(IEnumerable<IEnumerable<char>> name, Comment comment, bool isArrayOfTable)
            {
                this.Name = name.Select(x => string.Concat(x)).ToArray();
                this.Comment = comment;
                this.IsArrayOfTable = isArrayOfTable;
            }
        }

        internal abstract class TableNode { }

        internal class Comment : TableNode
        {
            public readonly string Text;

            public Comment(IEnumerable<char> text)
            {
                this.Text = string.Concat(text);
            }
        }

        internal class TomlValue
        {
            public readonly TomlItemType Type;
            public readonly object Value;

            public TomlValue(TomlItemType type, object value)
            {
                this.Type = type;
                this.Value = value;
            }
        }

        internal class ArrayItem
        {
            public readonly TomlValue Value;
            public readonly IEnumerable<Comment> Before;
            public readonly IEnumerable<Comment> After;

            public ArrayItem(TomlValue value, IEnumerable<Comment> before, IEnumerable<Comment> after)
            {
                this.Value = value;
                this.Before = before ?? Enumerable.Empty<Comment>();
                this.After = after ?? Enumerable.Empty<Comment>();
            }
        }

        internal class KeyValue : TableNode
        {
            public readonly string Key;
            public readonly TomlValue Value;
            public readonly Comment Comment;

            public KeyValue(IEnumerable<char> key, TomlValue value, Comment comment)
            {
                this.Key = string.Concat(key);
                this.Value = value;
                this.Comment = comment;
            }
        }

        internal class Table
        {
            public readonly TableInfo Info;
            public readonly IEnumerable<TableNode> Content;

            public Table(TableInfo info, IEnumerable<TableNode> content)
            {
                this.Info = info;
                this.Content = content;
            }
        }

        internal class ParseResult
        {
            public readonly IEnumerable<TableNode> RootNodes;
            public readonly IEnumerable<Table> Tables;

            public ParseResult(IEnumerable<TableNode> rootNodes, IEnumerable<Table> tables)
            {
                this.RootNodes = rootNodes;
                this.Tables = tables;
            }
        }

        private static string RemoveFirstNewLine(this string s)
        {
            if (s.Length > 0)
            {
                switch (s[0])
                {
                    case '\r':
                        return s.Substring(s.Length > 1 && s[1] == '\n' ? 2 : 1);
                    case '\n':
                        return s.Substring(1);
                }
            }
            return s;
        }

        private static Parser<char, ParseResult> v03Parser;
        private static Parser<char, ParseResult> v04Parser;

        private static Parser<TToken, Unit> SeqIgnore<TToken, T0, T1>(this Parser<TToken, T0> parser0, Parser<TToken, T1> parser1)
        {
            return parser0.Pipe(parser1, (_, __) => Unit.Instance);
        }

        private static void InitializeV03Parser()
        {
            var space = Chars.OneOf('\t', ' ').Ignore();
            var spaces = space.Many0().Ignore();
            var spacesOrNewlines = Chars.OneOf('\t', ' ', '\r', '\n').Many0().Ignore();
            var newline = Combinator.Choice(
                Chars.Sequence("\r\n"),
                Chars.Sequence("\r"),
                Chars.Sequence("\n")
            ).Ignore();
            var newlineOrEof = newline.Or(Chars.EndOfInput()).Ignore();
            var notNewlineChar = Chars.NoneOf('\r', '\n');

            var comment = notNewlineChar.Many0()
                .Between(Chars.Satisfy('#').Ignore(), newlineOrEof)
                .Select(c => new Comment(c));

            var tableNameChar = Chars.NoneOf('\t', ' ', '.', '\r', '\n', ']'); // should add '#'?
            var newlineOrComment = newlineOrEof.Select(_ => (Comment)null).Or(comment);

            var tableName = tableNameChar.Many1().SepBy1(Chars.Satisfy('.').Ignore())
                .Between(
                    spacesOrNewlines.SeqIgnore(Chars.Satisfy('[')),
                    Chars.Satisfy(']').SeqIgnore(spaces)
                );
            var tableNameLine = from t in tableName
                                from c in newlineOrComment
                                select new TableInfo(t, c, false);

            var arrayOfTableName = tableNameChar.Many1().SepBy1(Chars.Satisfy('.').Ignore())
                .Between(
                    spacesOrNewlines.SeqIgnore(Chars.Sequence("[[")),
                    Chars.Sequence("]]").SeqIgnore(spaces)
                );
            var arrayOfTableNameLine = from t in arrayOfTableName
                                       from c in newlineOrComment
                                       select new TableInfo(t, c, true);

            var escaped = Chars.Satisfy('\\').Bindr(Combinator.Choice(
                Chars.Satisfy('b').Select(_ => "\b"),
                Chars.Satisfy('t').Select(_ => "\t"),
                Chars.Satisfy('n').Select(_ => "\n"),
                Chars.Satisfy('f').Select(_ => "\f"),
                Chars.Satisfy('r').Select(_ => "\r"),
                Chars.Sequence("\""),
                Chars.Sequence("/"),
                Chars.Sequence("\\"),
                Chars.Satisfy('u').Bindr(Chars.Hex().Repeat(4))
                    .Or(Chars.Satisfy('U').Bindr(Chars.Hex().Repeat(8)))
                    .Select(c => char.ConvertFromUtf32(Convert.ToInt32(string.Concat(c), 16)))
            ));

            var newlineEscape = Combinator.Choice(Chars.Sequence("\\\r\n"), Chars.Sequence("\\\r"), Chars.Sequence("\\\n"))
                .Bindr(spacesOrNewlines).Select(_ => "");

            var basicString = Chars.NoneOf('\r', '\n', '"', '\\').Select(c => c.ToString())
                .Or(escaped).Many0().Between(Chars.Satisfy('"').Ignore(), Chars.Satisfy('"').Ignore())
                .Select(c => new TomlValue(TomlItemType.BasicString, c.Unfold()));

            var threeQuotes = Chars.Sequence("\"\"\"").Ignore();
            var multilineBasicStringChar = Combinator.Choice(
                Chars.NoneOf('\\', '"').Select(c => c.ToString()),
                escaped, newlineEscape
            );
            var multilineBasicString = Combinator.Choice(
                    multilineBasicStringChar,
                    Combinator.Sequence(Chars.Sequence('"'), multilineBasicStringChar).Select(Unfold),
                    Combinator.Sequence(Chars.Sequence("\"\""), multilineBasicStringChar).Select(Unfold)
                ).Many0().Between(threeQuotes, threeQuotes)
                .Select(c => new TomlValue(TomlItemType.MultilineBasicString, c.Unfold().RemoveFirstNewLine()));

            var literalString = Chars.NoneOf('\r', '\n', '\'').Many0()
                .Between(Chars.Satisfy('\'').Ignore(), Chars.Satisfy('\'').Ignore())
                .Select(c => new TomlValue(TomlItemType.LiteralString, string.Concat(c)));

            var threeLiteralQuotes = Chars.Sequence("'''").Ignore();
            var multilineLiteralStringChar = Chars.NoneOf('\'');
            var multilineLiteralString = Combinator.Choice(
                    multilineLiteralStringChar.Select(c => c.ToString()),
                    Combinator.Sequence(Chars.Satisfy('\''), multilineLiteralStringChar),
                    Chars.Sequence("''").Bindr(multilineLiteralStringChar).Select(c => string.Concat("''", c))
                ).Many0().Between(threeLiteralQuotes, threeLiteralQuotes)
                .Select(c => new TomlValue(TomlItemType.MultilineLiteralString, c.Unfold().RemoveFirstNewLine()));

            var sign = Chars.OneOf('+', '-').Optional().Select(o => o.Case(() => "", c => c.ToString()));
            var digit = Chars.Satisfy(c => c >= '0' && c <= '9');
            var digits = digit.Many1().Map(x => string.Concat(x));

            var integer = from s in sign
                          from i in digits
                          select new TomlValue(TomlItemType.Integer, string.Concat(s, i));

            var floatv = from s in sign
                         from i in digits
                         from d in Chars.Satisfy('.').Bindr(digits)
                         from e in Chars.OneOf('e', 'E').Bindr(digits)
                             .Optional().Map(x => x.HasValue ? ("e" + x.Value) : "")
                         select new TomlValue(TomlItemType.Float, string.Concat(s, i, ".", d, e));

            var boolv = Chars.Sequence("true").Select(_ => true)
                .Or(Chars.Sequence("false").Select(_ => false))
                .Select(b => new TomlValue(TomlItemType.Boolean, b));

            var hyphen = Chars.Satisfy('-');
            var colon = Chars.Satisfy(':');
            var twoDigits = digit.Repeat(2);
            var datetime = digit.Repeat(4) // year
                .Append(hyphen).Append(twoDigits) // month
                .Append(hyphen).Append(twoDigits) // day
                .Append(
                    Chars.Sequence("T").Append(twoDigits) // hour
                        .Append(colon).Append(twoDigits) // minute
                        .Append(colon).Append(twoDigits) // second
                        .Append(
                            Chars.Sequence(".").Append(digits).Optional()
                        )
                        .Append(
                            Chars.Sequence("Z")
                                .Or(
                                    Chars.Sequence("+").Or(Chars.Sequence("-"))
                                        .Append(twoDigits)
                                        .Append(Chars.Satisfy(':').Optional().Map(x => ':'))
                                        .Append(twoDigits)
                                )
                                .Optional()
                        )
                        .Optional()
                )
                .Map(x => new TomlValue(TomlItemType.Datetime, XmlConvert.ToDateTimeOffset(string.Concat(x))));

            Parser<char, TomlValue> arrayRef = null;
            var array = Delayed.Return(() => arrayRef);
            var comments = comment.Between(spacesOrNewlines, spacesOrNewlines).Many0();
            var comma = Chars.Satisfy(',').Between(spacesOrNewlines, spacesOrNewlines).Ignore();
            Func<Parser<char, TomlValue>, Parser<char, TomlValue>> createArrayParser = p =>
                Chars.Satisfy('[').Bindr(
                    from i in
                        (from b in comments
                         from v in p.Between(spacesOrNewlines, spacesOrNewlines)
                         from a in comments
                         select new ArrayItem(v, b, a)
                        ).SepEndBy0(comma)
                    from c in comments.Bindl(Chars.Satisfy(']'))
                    select new TomlValue(TomlItemType.Array, i.Concat(new[] { new ArrayItem(null, null, c) }))
                );
            arrayRef = Combinator.Choice(
                createArrayParser(Combinator.Choice(
                    multilineBasicString, basicString, multilineLiteralString, literalString)), // 順番大事
                createArrayParser(datetime),
                createArrayParser(integer),
                createArrayParser(floatv),
                createArrayParser(boolv),
                createArrayParser(Combinator.Lazy(array))
            );

            var value = Combinator.Choice(
                multilineBasicString, basicString, multilineLiteralString, literalString, // 順番大事
                datetime, integer, floatv, boolv, array.Force()
            );
            var keyValue = from k in Chars.NoneOf('\t', ' ', '\r', '\n', '=', '#').Many1().Between(spacesOrNewlines, spaces)
                           from v in Chars.Satisfy('=').Bindr(value.Between(spaces, spaces))
                           from c in newlineOrComment
                           select (TableNode)new KeyValue(k, v, c);

            var nodes = Combinator.Choice(keyValue, spacesOrNewlines.Bindr(comment)).Many0();
            var table = from t in Combinator.Choice(tableNameLine, arrayOfTableNameLine)
                        from c in nodes
                        select new Table(t, c);

            v03Parser = from r in nodes
                        from t in table.Many0().Bindl(spacesOrNewlines)
                        select new ParseResult(r, t);
        }

        private static void InitializeV04Parser()
        {
            var space = Chars.OneOf('\t', ' ').Ignore();
            var spaces = space.Many0().Ignore();
            var spacesOrNewlines = Chars.OneOf('\t', ' ', '\r', '\n').Many0().Ignore();
            var newline = Combinator.Choice(
                Chars.Sequence("\r\n"),
                Chars.Sequence("\r"),
                Chars.Sequence("\n")
            ).Ignore();
            var newlineOrEof = newline.Or(Chars.EndOfInput()).Ignore();
            var notNewlineChar = Chars.NoneOf('\r', '\n');

            var comment = notNewlineChar.Many0()
                .Between(Chars.Satisfy('#').Ignore(), newlineOrEof)
                .Select(c => new Comment(c));
            var newlineOrComment = newlineOrEof.Select(_ => (Comment)null).Or(comment);

            var escaped = Chars.Satisfy('\\').Bindr(Combinator.Choice(
                Chars.Satisfy('b').Select(_ => "\b"),
                Chars.Satisfy('t').Select(_ => "\t"),
                Chars.Satisfy('n').Select(_ => "\n"),
                Chars.Satisfy('f').Select(_ => "\f"),
                Chars.Satisfy('r').Select(_ => "\r"),
                Chars.Sequence("\""),
                //Chars.Sequence("/"),
                Chars.Sequence("\\"),
                Chars.Satisfy('u').Bindr(Chars.Hex().Repeat(4))
                    .Or(Chars.Satisfy('U').Bindr(Chars.Hex().Repeat(8)))
                    .Select(c => char.ConvertFromUtf32(Convert.ToInt32(string.Concat(c), 16)))
            ));

            var newlineEscape = Combinator.Choice(Chars.Sequence("\\\r\n"), Chars.Sequence("\\\r"), Chars.Sequence("\\\n"))
                .Bindr(spacesOrNewlines).Select(_ => "");

            var basicString = Chars.NoneOf('\r', '\n', '"', '\\').Select(c => c.ToString())
                .Or(escaped).Many0().Between(Chars.Satisfy('"').Ignore(), Chars.Satisfy('"').Ignore())
                .Select(c => new TomlValue(TomlItemType.BasicString, c.Unfold()));

            var threeQuotes = Chars.Sequence("\"\"\"").Ignore();
            var multilineBasicStringChar = Combinator.Choice(
                Chars.NoneOf('\\', '"').Select(c => c.ToString()),
                escaped, newlineEscape
            );
            var multilineBasicString = Combinator.Choice(
                    multilineBasicStringChar,
                    Combinator.Sequence(Chars.Sequence('"'), multilineBasicStringChar).Select(Unfold),
                    Combinator.Sequence(Chars.Sequence("\"\""), multilineBasicStringChar).Select(Unfold)
                ).Many0().Between(threeQuotes, threeQuotes)
                .Select(c => new TomlValue(TomlItemType.MultilineBasicString, c.Unfold().RemoveFirstNewLine()));

            var literalString = Chars.NoneOf('\r', '\n', '\'').Many0()
                .Between(Chars.Satisfy('\'').Ignore(), Chars.Satisfy('\'').Ignore())
                .Select(c => new TomlValue(TomlItemType.LiteralString, string.Concat(c)));

            var threeLiteralQuotes = Chars.Sequence("'''").Ignore();
            var multilineLiteralStringChar = Chars.NoneOf('\'');
            var multilineLiteralString = Combinator.Choice(
                    multilineLiteralStringChar.Select(c => c.ToString()),
                    Combinator.Sequence(Chars.Satisfy('\''), multilineLiteralStringChar),
                    Chars.Sequence("''").Bindr(multilineLiteralStringChar).Select(c => string.Concat("''", c))
                ).Many0().Between(threeLiteralQuotes, threeLiteralQuotes)
                .Select(c => new TomlValue(TomlItemType.MultilineLiteralString, c.Unfold().RemoveFirstNewLine()));

            var sign = Chars.OneOf('+', '-').Optional().Select(o => o.Case(() => "", c => c.ToString()));
            var digit = Chars.Satisfy(c => c >= '0' && c <= '9');
            var digits = digit.Many1();
            var digitsWithUnderscores = digits.SepBy1(Chars.Satisfy('_').Ignore()).Map(Unfold);

            var integer = from s in sign
                          from i in digitsWithUnderscores
                          select new TomlValue(TomlItemType.Integer, string.Concat(s, i));

            var floatv = from s in sign
                         from i in digitsWithUnderscores
                         from d in Chars.Satisfy('.').Bindr(digitsWithUnderscores)
                         from e in Chars.OneOf('e', 'E').Bindr(digitsWithUnderscores)
                             .Optional().Map(x => x.HasValue ? ("e" + x.Value) : "")
                         select new TomlValue(TomlItemType.Float, string.Concat(s, i, ".", d, e));

            var boolv = Chars.Sequence("true").Select(_ => true)
                .Or(Chars.Sequence("false").Select(_ => false))
                .Select(b => new TomlValue(TomlItemType.Boolean, b));

            var hyphen = Chars.Satisfy('-');
            var colon = Chars.Satisfy(':');
            var twoDigits = digit.Repeat(2);
            var datetime = digit.Repeat(4) // year
                .Append(hyphen).Append(twoDigits) // month
                .Append(hyphen).Append(twoDigits) // day
                .Append(
                    Chars.Sequence("T").Append(twoDigits) // hour
                        .Append(colon).Append(twoDigits) // minute
                        .Append(colon).Append(twoDigits) // second
                        .Append(
                            Chars.Sequence(".").Append(digits).Optional()
                        )
                        .Append(
                            Chars.Sequence("Z")
                                .Or(
                                    Chars.Sequence("+").Or(Chars.Sequence("-"))
                                        .Append(twoDigits)
                                        .Append(Chars.Satisfy(':'))
                                        .Append(twoDigits)
                                )
                                .Optional()
                        )
                        .Optional()
                )
                .Map(x => new TomlValue(TomlItemType.Datetime, XmlConvert.ToDateTimeOffset(string.Concat(x))));

            Parser<char, TomlValue> arrayRef = null;
            var array = Delayed.Return(() => arrayRef);
            Parser<char, TomlValue> inlineTableRef = null;
            var inlineTable = Delayed.Return(() => inlineTableRef);

            var key = Chars.Satisfy(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                .Many1().Or(basicString.Map(x => (string)x.Value));
            var value = Combinator.Choice(
                multilineBasicString, basicString, multilineLiteralString, literalString, // 順番大事
                datetime, integer, floatv, boolv, Combinator.Lazy(array), Combinator.Lazy(inlineTable)
            );

            inlineTableRef = key.Between(spaces, spaces)
                .Pipe(
                    Chars.Satisfy('=').Bindr(value.Between(spaces, spaces)),
                    (k, v) => new KeyValue(k, v, null)
                )
                .SepEndBy0(Chars.Satisfy(',').Ignore())
                .Between(Chars.Satisfy('{').Ignore(), spaces.SeqIgnore(Chars.Satisfy('}')))
                .Map(x => new TomlValue(TomlItemType.InlineTable, x));

            var comments = comment.Between(spacesOrNewlines, spacesOrNewlines).Many0();
            var comma = Chars.Satisfy(',').Between(spacesOrNewlines, spacesOrNewlines).Ignore();
            Func<Parser<char, TomlValue>, Parser<char, TomlValue>> createArrayParser = p =>
                Chars.Satisfy('[').Bindr(
                    from i in
                        (from b in comments
                         from v in p.Between(spacesOrNewlines, spacesOrNewlines)
                         from a in comments
                         select new ArrayItem(v, b, a)
                        ).SepEndBy0(comma)
                    from c in comments.Bindl(Chars.Satisfy(']'))
                    select new TomlValue(TomlItemType.Array, i.Concat(new[] { new ArrayItem(null, null, c) }))
                );
            arrayRef = Combinator.Choice(
                createArrayParser(Combinator.Choice(
                    multilineBasicString, basicString, multilineLiteralString, literalString)), // 順番大事
                createArrayParser(datetime),
                createArrayParser(integer),
                createArrayParser(floatv),
                createArrayParser(boolv),
                createArrayParser(Combinator.Lazy(array)),
                createArrayParser(Combinator.Lazy(inlineTable))
            );

            var tableName = key.Between(spaces, spaces).SepBy1(Chars.Satisfy('.').Ignore());

            var startTable = tableName.Between(
                spacesOrNewlines.SeqIgnore(Chars.Satisfy('[')),
                Chars.Satisfy(']').SeqIgnore(spaces)
            );
            var startTableLine = from t in startTable
                                 from c in newlineOrComment
                                 select new TableInfo(t, c, false);

            var startArrayOfTable = tableName.Between(
                spacesOrNewlines.SeqIgnore(Chars.Sequence("[[")),
                Chars.Sequence("]]").SeqIgnore(spaces)
            );
            var startArrayOfTableLine = from t in startArrayOfTable
                                        from c in newlineOrComment
                                        select new TableInfo(t, c, true);

            var keyValue = from k in key.Between(spacesOrNewlines, spaces)
                           from v in Chars.Satisfy('=').Bindr(value.Between(spaces, spaces))
                           from c in newlineOrComment
                           select (TableNode)new KeyValue(k, v, c);
            var nodes = Combinator.Choice(keyValue, spacesOrNewlines.Bindr(comment)).Many0();
            var table = from t in Combinator.Choice(startTableLine, startArrayOfTableLine)
                        from c in nodes
                        select new Table(t, c);

            v04Parser = from r in nodes
                        from t in table.Many0().Bindl(spacesOrNewlines)
                        select new ParseResult(r, t);
        }

        internal static Parser<char, ParseResult> V03Parser
        {
            get
            {
                if (v03Parser == null)
                    InitializeV03Parser();
                return v03Parser;
            }
        }

        internal static Parser<char, ParseResult> V04Parser
        {
            get
            {
                if (v04Parser == null)
                    InitializeV04Parser();
                return v04Parser;
            }
        }

        private static IEnumerable<object> ConvertContent(TomlValue value)
        {
            switch (value.Type)
            {
                case TomlItemType.Array:
                    foreach (var a in (IEnumerable<ArrayItem>)value.Value)
                    {
                        foreach (var c in a.Before)
                            yield return new XComment(c.Text);

                        if (a.Value != null)
                            yield return new XElement("item",
                                new XAttribute("type", XUtils.GetJsonTypeString(a.Value.Type)),
                                new XAttribute("toml", a.Value.Type.ToString()),
                                ConvertContent(a.Value)
                            );

                        foreach (var c in a.After)
                            yield return new XComment(c.Text);
                    }
                    break;
                case TomlItemType.InlineTable:
                    foreach (var n in ToXNodes((IEnumerable<KeyValue>)value.Value))
                        yield return n;
                    break;
                default:
                    yield return value.Value;
                    break;
            }
        }

        private static IEnumerable<XNode> ToXNodes(IEnumerable<TableNode> nodes)
        {
            foreach (var n in nodes.Where(n => n != null))
            {
                var kv = n as KeyValue;
                if (kv != null)
                {
                    var tomlType = new XAttribute("toml", kv.Value.Type.ToString());
                    var jsonType = new XAttribute("type", XUtils.GetJsonTypeString(kv.Value.Type));
                    var content = ConvertContent(kv.Value);
                    yield return XUtils.IsValidName(kv.Key)
                        ? new XElement(kv.Key, jsonType, tomlType, content)
                        : new XElement(XUtils.NamespaceA + "item",
                            XUtils.PrefixA, new XAttribute("item", kv.Key),
                            jsonType, tomlType, content
                        );

                    if (kv.Comment != null)
                        yield return new XComment(kv.Comment.Text);
                }
                else
                {
                    yield return new XComment(((Comment)n).Text);
                }
            }
        }

        private static XElement CreateTableElement(string name, IEnumerable<TableNode> nodes)
        {
            return XUtils.CreateElement(name,
                new XAttribute("type", "object"),
                new XAttribute("toml", TomlItemType.Table.ToString()),
                ToXNodes(nodes)
            );
        }

        private class TableTree
        {
            public readonly string[] FullName;
            public readonly IEnumerable<TableNode> Nodes;
            public readonly Dictionary<string, List<TableTree>> ArrayOfTables = new Dictionary<string, List<TableTree>>();
            public readonly Dictionary<string, TableTree> Children = new Dictionary<string, TableTree>();

            public TableTree(string[] fullName, IEnumerable<TableNode> nodes)
            {
                this.FullName = fullName;
                this.Nodes = nodes ?? Enumerable.Empty<TableNode>();
            }

            public XElement ToXElement(string name)
            {
                var xe = CreateTableElement(name, this.Nodes);
                foreach (var kvp in this.ArrayOfTables)
                {
                    xe.Add(XUtils.CreateElement(kvp.Key,
                        new XAttribute("type", "array"),
                        new XAttribute("toml", TomlItemType.Array.ToString()),
                        kvp.Value.Select(t => t.ToXElement("item"))
                    ));
                }
                xe.Add(this.Children.Select(kvp => kvp.Value.ToXElement(kvp.Key)));
                return xe;
            }
        }

        private static FormatException ThrowFormatException(string message, ITokenStream<char> stream)
        {
            throw new FormatException(string.Join(" ", message,
                stream.Current.Case(() => "", x => x.Item1.Convert((line, col) => string.Format("Line:{0} Column:{1}", line, col)))));
        }

        internal static XElement DeserializeXElement(this Parser<char, ParseResult> parser, ITokenStream<char> stream)
        {
            var result = parser(stream).Case(
                (s, err) =>
                {
                    ThrowFormatException("Parse failed: " + err, s);
                    return null;
                },
                (s, r) => Tuple.Create(s, r)
            );

            if (result.Item1.MoveNext().Current.HasValue)
                ThrowFormatException("Parse stopped", result.Item1);

            var root = new TableTree(new string[0], result.Item2.RootNodes);

            foreach (var t in result.Item2.Tables)
            {
                var current = root;
                var name = t.Info.Name;
                for (var i = 0; i < name.Length - 1; i++)
                {
                    var currentName = name[i];
                    if (current.ArrayOfTables.ContainsKey(currentName))
                        current = current.ArrayOfTables[currentName].Last();
                    else if (current.Children.ContainsKey(currentName))
                        current = current.Children[currentName];
                    else
                    {
                        var newt = new TableTree(name.Take(i + 1).ToArray(), null);
                        current.Children.Add(currentName, newt);
                        current = newt;
                    }
                }

                var lastName = name[name.Length - 1];
                if (t.Info.IsArrayOfTable)
                {
                    List<TableTree> arrayOfTables;
                    if (!current.ArrayOfTables.TryGetValue(lastName, out arrayOfTables))
                    {
                        arrayOfTables = new List<TableTree>();
                        current.ArrayOfTables.Add(lastName, arrayOfTables);
                    }
                    arrayOfTables.Add(new TableTree(t.Info.Name, t.Content));
                }
                else
                {
                    current.Children.Add(lastName, new TableTree(t.Info.Name, t.Content));
                }
            }

            return root.ToXElement("root");
        }
    }
}
