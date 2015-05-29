namespace HyperTomlProcessor.Test
{
    public static class Examples
    {
        //The MIT License

        //Copyright (c) Tom Preston-Werner

        //Permission is hereby granted, free of charge, to any person obtaining a copy
        //of this software and associated documentation files (the "Software"), to deal
        //in the Software without restriction, including without limitation the rights
        //to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        //copies of the Software, and to permit persons to whom the Software is
        //furnished to do so, subject to the following conditions:

        //The above copyright notice and this permission notice shall be included in
        //all copies or substantial portions of the Software.

        //THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        //IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        //FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        //AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        //LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        //OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
        //THE SOFTWARE.

        public const string Example = @"# This is a TOML document. Boom.

title = ""TOML Example""

[owner]
name = ""Tom Preston-Werner""
organization = ""GitHub""
bio = ""GitHub Cofounder & CEO\nLikes tater tots and beer.""
dob = 1979-05-27T07:32:00Z # First class dates? Why not?

[database]
server = ""192.168.1.1""
ports = [ 8001, 8001, 8002 ]
connection_max = 5000
enabled = true

[servers]

  # You can indent as you please. Tabs or spaces. TOML don't care.
  [servers.alpha]
  ip = ""10.0.0.1""
  dc = ""eqdc10""

  [servers.beta]
  ip = ""10.0.0.2""
  dc = ""eqdc10""
  country = ""中国"" # This should be parsed as UTF-8

[clients]
data = [ [""gamma"", ""delta""], [1, 2] ] # just an update to make sure parsers support it

# Line breaks are OK when inside arrays
hosts = [
  ""alpha"",
  ""omega""
]

# Products

  [[products]]
  name = ""Hammer""
  sku = 738594937

  [[products]]
  name = ""Nail""
  sku = 284758393
  color = ""gray""";

        public const string HardExample = @"# Test file for TOML
# Only this one tries to emulate a TOML file written by a user of the kind of parser writers probably hate
# This part you'll really hate

[the]
test_string = ""You'll hate me after this - #""          # "" Annoying, isn't it?

    [the.hard]
    test_array = [ ""] "", "" # ""]      # ] There you go, parse this!
    test_array2 = [ ""Test #11 ]proved that"", ""Experiment #9 was a success"" ]
    # You didn't think it'd as easy as chucking out the last #, did you?
    another_test_string = "" Same thing, but with a string #""
    harder_test_string = "" And when \""'s are in the string, along with # \""""   # ""and comments are there too""
    # Things will get harder
    
        [the.hard.""bit#""]
        ""what?"" = ""You don't think some user won't do that?""
        multi_line_array = [
            ""]"",
            # ] Oh yes I did
            ]";

        public const string Error0 = "[error]   if you didn't catch this, your parser is broken";
        public const string Error1 = @"string = ""Anything other than tabs, spaces and newline after a keygroup or key value pair has ended should produce an error unless it is a comment""   like this";
        public const string Error2 = @"array = [
         ""This might most likely happen in multiline arrays"",
         Like here,
         ""or here,
         and here""
         ]     End of array comment, forgot the #";
        public const string Error3 = "number = 3.14  pi <--again forgot the #         ";
    }
}
