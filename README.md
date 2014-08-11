# HyperTomlProcessor #
.NET 向け TOML パーサーです。 [2014 年 8 月時点で最新の TOML の仕様](https://github.com/toml-lang/toml/blob/d2ba658229a5188e639302c4e8929035786f094a/README.md)に対応しています。

# 特徴 #
- LINQ to XML で TOML を操作することが出来ます
- DynamicJson 互換の API で操作することが出来ます
- DataContractJsonSerializer を用いてシリアライズ / デシリアライズが行えます（ラップ済み）

# 対応プラットフォーム #
- .NET Framework 4.0 - 4.5

その他のプラットフォームについては要望次第で調査します。いまのところ PCL にしたらビルド通らなくてやる気なくした。

# インストール #
```
PM> Install-Package HyperTomlProcessor
```

# XElement に変換して操作する #
[TomlConvert.DeserializeXElement](http://azyobuzi.net/HyperTomlProcessor/html/03bd5ce3-7346-c882-2685-4533713288c4.htm) メソッドを使用して TOML 文字列またはストリームから `XElement` に変換します。

変換された `XElement` とその子・孫要素には `type` と `toml` という属性がついています。
`type` 属性は [JsonReaderWriterFactory のもの](http://msdn.microsoft.com/ja-jp/library/bb924435.aspx)と互換性があります。
`toml` 属性の値は内容によって `BasicString`, `MultilineBasicString`, `LiteralString`, `MultilineLiteralString`, `Integer`, `Float`, `Boolean`, `Datetime`, `Array`, `Table` のいずれかになります。

[TomlConvert.SerializeXElement](http://azyobuzi.net/HyperTomlProcessor/html/31c1469e-26e3-1219-58d1-1d7627f5f973.htm) メソッドを使用して TOML の内容を表す `XElement` から TOML 形式の文字列を作成します。
TOML におけるデータの種類は `type` 属性や `toml` 属性から決定します。

# DataContractJsonSerializer を使用してシリアライズ / デシリアライズを行う #
`DataContractJsonSerializer` によるシリアライズ / デシリアライズ処理をそれぞれ [TomlConvert.SerializeObject](http://azyobuzi.net/HyperTomlProcessor/html/cbb9d60a-8c16-5532-505c-35048270745f.htm) メソッド、 [TomlConvert.DeserializeObject](http://azyobuzi.net/HyperTomlProcessor/html/427e99a0-b47d-ec00-e711-ae0d8e1f1027.htm) メソッドで行えます。簡単なラッパーメソッドとなっているので動作の拡張についてはソースコードを参考にしてください。

# dynamic を使用した操作 #
[DynamicToml](http://azyobuzi.net/HyperTomlProcessor/html/59f27c6a-7a3b-f1d6-7cbe-3a2d32af086b.htm) クラスでは [DynamicJson](https://dynamicjson.codeplex.com/) に似た API で TOML の内容を操作することができます。基本的には DynamicJson と同じ動作をするようになっていますが、提供しているメソッドが違うので、ドキュメントを参照してください。また `XElement` にキャストすることで内部で使用している `XElement` インスタンスを取得できます。