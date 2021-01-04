﻿/*
 * Copyright © 2020 robby & EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */

using System;

namespace BaseUtils.JSON
{
    public partial class JToken 
    {
        // null if its unhappy and error is set
        // decoder does not worry about extra text after the object.

        [Flags]
        public enum ParseOptions
        {
            None = 0,
            AllowTrailingCommas = 1,
            CheckEOL = 2,
        }

        public static JToken Parse(string s, ParseOptions flags = ParseOptions.None)        // null if failed - must not be extra text
        {
            StringParserQuick parser = new StringParserQuick(s);
            var res = Parse(parser, out string unused, flags,s.Length);
            return ((flags & ParseOptions.CheckEOL) != 0 && !parser.IsEOL()) ? null : res;
        }

        public static JToken Parse(string s, out string error, ParseOptions flags = ParseOptions.None)
        {
            StringParserQuick parser = new StringParserQuick(s);
            JToken res = Parse(parser, out error, flags, s.Length);
            return ((flags & ParseOptions.CheckEOL) != 0 && !parser.IsEOL()) ? null : res;
        }

        public static JToken Parse(System.IO.TextReader trx, out string error, ParseOptions flags = ParseOptions.None, int chunksize = 16384, int textmaxsize=16384)
        {
            StringParserQuickTextReader parser = new StringParserQuickTextReader(trx, chunksize);
            JToken res = Parse(parser, out error, flags, textmaxsize);
            return ((flags & ParseOptions.CheckEOL) != 0 && !parser.IsEOL()) ? null : res;
        }

        // normally, use Parse above. Used only if you want to feed in another type of parser..

        public static JToken Parse(IStringParserQuick parser, out string error, ParseOptions flags, int textbufsize)
        {
            error = null;

            JToken[] stack = new JToken[256];
            int sptr = 0;
            bool comma = false;
            JArray curarray = null;
            JObject curobject = null;

            char[] textbuffer = new char[textbufsize];      // textbuffer to use for string decodes - one get of it, multiple reuses, faster

            // first decode the first value/object/array
            {
                JToken o = DecodeValue(parser, textbuffer, false);       // grab new value, not array end

                if (o == null)
                {
                    error = GenError(parser,"No Obj/Array");
                    return null;
                }
                else if (o.TokenType == TType.Array)
                {
                    stack[++sptr] = o;                      // push this one onto stack
                    curarray = o as JArray;                 // this is now the current array
                }
                else if (o.TokenType == TType.Object)
                {
                    stack[++sptr] = o;                      // push this one onto stack
                    curobject = o as JObject;               // this is now the current object
                }
                else
                {
                    return o;                               // value only
                }
            }

            while (true)
            {
                if (curobject != null)      // if object..
                {
                    while (true)
                    {
                        char next = parser.GetChar();

                        if (next == '}')    // end object
                        {
                            parser.SkipSpace();

                            if (comma == true && (flags & ParseOptions.AllowTrailingCommas) == 0)
                            {
                                error = GenError(parser, "Comma");
                                return null;
                            }
                            else
                            {
                                JToken prevtoken = stack[--sptr];
                                if (prevtoken == null)      // if popped stack is null, we are back to beginning, return this
                                {
                                    return stack[sptr + 1];
                                }
                                else
                                {
                                    comma = parser.IsCharMoveOn(',');
                                    curobject = prevtoken as JObject;
                                    if (curobject == null)
                                    {
                                        curarray = prevtoken as JArray;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (next == '"')   // property name
                        {
                            int textlen = parser.NextQuotedString(next, textbuffer, true);

                            if (textlen < 1 || (comma == false && curobject.Count > 0) || !parser.IsCharMoveOn(':'))
                            {
                                error = GenError(parser, "Object missing property name");
                                return null;
                            }
                            else
                            {
                                string name = new string(textbuffer, 0, textlen);

                                JToken o = DecodeValue(parser, textbuffer, false);      // get value

                                if (o == null)
                                {
                                    error = GenError(parser, "Object bad value");
                                    return null;
                                }

                                o.Name = name;                          // object gets the name, indicating its a property
                                curobject[name] = o;                    // assign to dictionary

                                if (o.TokenType == TType.Array)         // if array, we need to change to this as controlling object on top of stack
                                {
                                    if (sptr == stack.Length - 1)
                                    {
                                        error = GenError(parser, "Stack overflow");
                                        return null;
                                    }

                                    stack[++sptr] = o;                  // push this one onto stack
                                    curarray = o as JArray;             // this is now the current object
                                    curobject = null;
                                    comma = false;
                                    break;
                                }
                                else if (o.TokenType == TType.Object)   // if object, this is the controlling object
                                {
                                    if (sptr == stack.Length - 1)
                                    {
                                        error = GenError(parser, "Stack overflow");
                                        return null;
                                    }

                                    stack[++sptr] = o;                  // push this one onto stack
                                    curobject = o as JObject;           // this is now the current object
                                    comma = false;
                                }
                                else
                                {
                                    comma = parser.IsCharMoveOn(',');
                                }
                            }
                        }
                        else
                        {
                            error = GenError(parser,"Bad format in object");
                            return null;
                        }
                    }
                }
                else
                {
                    while (true)
                    {
                        JToken o = DecodeValue(parser, textbuffer, true);       // grab new value

                        if (o == null)
                        {
                            error = GenError(parser, "Bad array value");
                            return null;
                        }
                        else if (o.TokenType == TType.EndArray)          // if end marker, jump back
                        {
                            if (comma == true && (flags & ParseOptions.AllowTrailingCommas) == 0)
                            {
                                error = GenError(parser,"Comma");
                                return null;
                            }
                            else
                            {
                                JToken prevtoken = stack[--sptr];
                                if (prevtoken == null)      // if popped stack is null, we are back to beginning, return this
                                {
                                    return stack[sptr + 1];
                                }
                                else
                                {
                                    comma = parser.IsCharMoveOn(',');
                                    curobject = prevtoken as JObject;
                                    if (curobject == null)
                                    {
                                        curarray = prevtoken as JArray;
                                    }
                                    else
                                        break;
                                }
                            }
                        }
                        else if ((comma == false && curarray.Count > 0))   // missing comma
                        {
                            error = GenError(parser,"Comma");
                            return null;
                        }
                        else
                        {
                            curarray.Add(o);

                            if (o.TokenType == TType.Array) // if array, we need to change to this as controlling object on top of stack
                            {
                                if (sptr == stack.Length - 1)
                                {
                                    error = GenError(parser, "Stack overflow");
                                    return null;
                                }

                                stack[++sptr] = o;              // push this one onto stack
                                curarray = o as JArray;         // this is now the current array
                                comma = false;
                            }
                            else if (o.TokenType == TType.Object) // if object, this is the controlling object
                            {
                                if (sptr == stack.Length - 1)
                                {
                                    error = GenError(parser, "Stack overflow");
                                    return null;
                                }

                                stack[++sptr] = o;              // push this one onto stack
                                curobject = o as JObject;       // this is now the current object
                                curarray = null;
                                comma = false;
                                break;
                            }
                            else
                            {
                                comma = parser.IsCharMoveOn(',');
                            }
                        }
                    }
                }

            }
        }

        static JToken jendarray = new JToken(TType.EndArray);

        // return JObject, JArray, jendarray indicating end array if inarray is set, string, long, ulong, bigint, true, false, JNull
        // null if unhappy

        static private JToken DecodeValue(IStringParserQuick parser, char[] textbuffer, bool inarray)
        {
            //System.Diagnostics.Debug.WriteLine("Decode at " + p.LineLeft);
            char next = parser.GetChar();
            switch (next)
            {
                case '{':
                    parser.SkipSpace();
                    return new JObject();

                case '[':
                    parser.SkipSpace();
                    return new JArray();

                case '"':
                    int textlen = parser.NextQuotedString(next, textbuffer, true);
                    return textlen >= 0 ? new JToken(TType.String, new string(textbuffer, 0, textlen)) : null;

                case ']':
                    if (inarray)
                    {
                        parser.SkipSpace();
                        return jendarray;
                    }
                    else
                        return null;

                case '0':       // all positive. JSON does not allow a + at the start (integer fraction exponent)
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    parser.BackUp();
                    return parser.JNextNumber(false);
                case '-':
                    return parser.JNextNumber(true);
                case 't':
                    return parser.IsStringMoveOn("rue") ? new JToken(TType.Boolean, true) : null;
                case 'f':
                    return parser.IsStringMoveOn("alse") ? new JToken(TType.Boolean, false) : null;
                case 'n':
                    return parser.IsStringMoveOn("ull") ? new JToken(TType.Null) : null;

                default:
                    return null;
            }
        }

        static private string GenError(IStringParserQuick parser, string error)
        {
            string s = "JSON " + error + " at " + parser.Position + " " + parser.Line.Substring(0, parser.Position) + " <ERROR> "
                            + parser.Line.Substring(parser.Position);
            System.Diagnostics.Debug.WriteLine(s);
            return s;
        }
    }
}



