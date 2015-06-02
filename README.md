# HyperTomlProcessor #
.NET 向け TOML パーサーです。 v0.3.0, 0.3.1, 0.4.0 に対応しています。

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
[Toml.DeserializeXElement](https://azyobuzin.github.io/HyperTomlProcessor/html/5ADEF45B.htm) メソッドを使用して TOML 文字列またはストリームから `XElement` に変換します。

変換された `XElement` とその子・孫要素には `type` と `toml` という属性がついています。
`type` 属性は [JsonReaderWriterFactory のもの](http://msdn.microsoft.com/ja-jp/library/bb924435.aspx)と互換性があります。
`toml` 属性の値は内容によって `BasicString`, `MultilineBasicString`, `LiteralString`, `MultilineLiteralString`, `Integer`, `Float`, `Boolean`, `Datetime`, `Array`, `Table` のいずれかになります。

[Toml.SerializeXElement](https://azyobuzin.github.io/HyperTomlProcessor/html/D2A86D32.htm) メソッドを使用して TOML の内容を表す `XElement` から TOML 形式の文字列を作成します。
TOML におけるデータの種類は `type` 属性や `toml` 属性から決定します。

# DataContractJsonSerializer を使用してシリアライズ / デシリアライズを行う #
`DataContractJsonSerializer` によるシリアライズ / デシリアライズ処理をそれぞれ [Toml.SerializeObject](https://azyobuzin.github.io/HyperTomlProcessor/html/1AD356B9.htm) メソッド、 [Toml.DeserializeObject](https://azyobuzin.github.io/HyperTomlProcessor/html/586BBC40.htm) メソッドで行えます。簡単なラッパーメソッドとなっているので動作の拡張についてはソースコードを参考にしてください。

# dynamic を使用した操作 #
[DynamicToml](https://azyobuzin.github.io/HyperTomlProcessor/html/B95EFF51.htm) クラスでは [DynamicJson](https://dynamicjson.codeplex.com/) に似た API で TOML の内容を操作することができます。基本的には DynamicJson と同じ動作をするようになっていますが、提供しているメソッドが違うので、ドキュメントを参照してください。また `XElement` にキャストすることで内部で使用している `XElement` インスタンスを取得できます。
