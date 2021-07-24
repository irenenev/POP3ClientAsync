using System;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace POP3ClientAsync
{
    public class POP3EmailMessage
    {
        //номер письма
        public long msgNumber;
        //размер письма
        public long msgSize;
        //письмо получено?
        public bool msgReceived;
        //заголовки письма
        public string msgHeaders;
        //тело письма
        public string msgContent;

        //Метод декодирования письма
        public void ParseMail()
        {
            //декодирование заголовков
            //cp содержит кодировку, ct содержит B или Q, value содержит строку между знаками =?.....?=
            Regex regHeaders = new Regex(@"\=\?(?<cp>[\w\d\-]+)\?(?<ct>[\w]{1})\?(?<value>[\w\d\/\=\+]+)\?\=", RegexOptions.IgnoreCase);
            MatchCollection mc = regHeaders.Matches(msgHeaders);
            foreach (Match m in mc)
                if (m.Success)
                    msgHeaders = regHeaders.Replace(msgHeaders, HeadersEncode(m), 1);

            //регулярные выражения для поиска заголовков: \x22 - это кавычки; \x5f - это нижнее подчеркивание;
            Regex regCT = new Regex(@"Content-Type:\s(?<type>[\w\/\-]+)\;");
            Regex regCTE = new Regex(@"Content-Transfer-Encoding:\s(?<type>[\d\w\-]+)");
            Regex regChar = new Regex(@"charset\=([\x22]{0,1})(?<charset>[\d\w\-]+)([\x22]{0,1}?)", RegexOptions.IgnoreCase);
            Regex regBound = new Regex(@"boundary\=.?([\x22]{0,1})(?<bound>[\d\w\-\+\x5f\=\.\/]+)([\/]{0,1})([\x22]{0,1}?)");
            //заголовок Content-Type в Header
            string ContentTypeHeader = regCT.Match(msgHeaders).Groups["type"].Value;
            //граница фрагментов письма
            string Boundary = regBound.Match(msgHeaders).Groups["bound"].Value;
            if (!String.IsNullOrEmpty(Boundary))
                msgContent = SplitBody(Boundary, msgContent, regCT, regCTE, regChar, regBound);
        }

        //Метод разделения тела письма на фрагменты по boundary
        private string SplitBody(string boundary, string body, Regex regCT, Regex regCTE, Regex regChar, Regex regBound)
        {
            //список индексов вхождения boundary
            var indexes = new List<int>();
            //подстрока --boundary
            string boundstring = String.Format("--{0}", boundary);
            //поиск индекса вхождения substring в строке msgContent, начиная с позиции 0
            int index = body.IndexOf(boundstring, 0);
            //индекс до sub boundary
            int ind = index;
            //если индекс вхождения найден
            while (index > -1)
            {
                //добавляем индекс вхождения в список индексов
                indexes.Add(index);
                //ищем следующий индекс вхождения
                index = body.IndexOf(boundstring, index + boundstring.Length);
            }
            //количество фрагментов на 1 меньше найденного количества строк boundary, поэтому Count - 1
            //фрагменты тела письма, отделенные по boundary
            string[] FragmentBody = new String[indexes.Count - 1];
            //фрагменты письма для декодирования
            string[] FragmentToReplace = new String[indexes.Count - 1];
            //заголовки ContentType внутри фрагментов
            string[] ContentTypeBody = new String[indexes.Count - 1];
            //заголовки Content-Transfer-Encoding внутри фрагментов
            string[] ContentTransferEncoding = new String[indexes.Count - 1];
            //кодировки charset in Content-Type
            string[] Charset = new String[indexes.Count - 1];
            string newcontent = "";
            for (int k = 0; k < indexes.Count - 1; k++)
            {
                int len = indexes[k + 1] - indexes[k] - boundstring.Length;
                FragmentBody[k] = body.Substring(indexes[k] + boundstring.Length, len).Trim("\n\r".ToCharArray());
                //соответствие регулярным выражениям внутри фрагментов
                ContentTypeBody[k] = regCT.Match(FragmentBody[k]).Groups["type"].Value; //"text/plain";
                ContentTransferEncoding[k] = regCTE.Match(FragmentBody[k]).Groups["type"].Value; //"quoted-printable"; //"base64";
                Charset[k] = regChar.Match(FragmentBody[k]).Groups["charset"].Value; //"iso-8859-5";
                //Если составной ContentTypeBody
                if (ContentTypeBody[k].Contains("multipart"))
                {
                    //вложенная граница фрагментов письма
                    string SubBoundary = regBound.Match(FragmentBody[k]).Groups["bound"].Value;
                    if (!String.IsNullOrEmpty(SubBoundary))
                        FragmentBody[k] = SplitBody(SubBoundary, FragmentBody[k], regCT, regCTE, regChar, regBound);
                }
                //позиция закодированного содержимого внутри фрагмента тела письма
                int textPos = FragmentBody[k].IndexOf("\r\n\r\n");
                if (textPos != -1)
                    //закодированный фрагмент тела письма
                    FragmentToReplace[k] = FragmentBody[k].Substring(textPos + 4, FragmentBody[k].Length - textPos - 4);
                else FragmentBody[k] = FragmentBody[k] + "\r\n";
                if (!String.IsNullOrEmpty(FragmentToReplace[k]))
                {
                    if (!String.IsNullOrEmpty(Charset[k]))
                        FragmentToReplace[k] = DecodeContent(ContentTransferEncoding[k], FragmentToReplace[k], Charset[k]);
                    //Если во вложении файл, картинка, аудио, видео
                    if (ContentTypeBody[k].Contains("application") || ContentTypeBody[k].Contains("image") || ContentTypeBody[k].Contains("audio") || ContentTypeBody[k].Contains("video"))
                    {
                        string Name = "";
                        //незакодированное имя
                        Regex regName = new Regex(@"name\=.?([\x22]{0,1})(?<name>[\d\w]+[\.]{1}[\w]+).?([\x22]{0,1})");
                        Match mName = regName.Match(FragmentBody[k]);
                        if (mName.Success)
                            Name = mName.Groups["name"].Value;
                        //закодированное имя
                        Match mCodedName = Regex.Match(FragmentBody[k], @"name\=.?([\x22]{0,1})\=\?(?<cp>[\w\d\-]+)\?(?<ct>[\w]{1})\?(?<value>[\w\d\=]+)\?\=", RegexOptions.IgnoreCase);
                        if (mCodedName.Success)
                        {
                            //раскодирование имени вложения
                            Name = HeadersEncode(mCodedName);
                            //замена закодированного имени на раскодированное значение
                            FragmentBody[k] = Regex.Replace(FragmentBody[k], @"name\=.?([\x22]{0,1})\=\?([\w\d\-]+)\?([\w]{1})\?([\w\d\=]+)\?\=", "name=\"" + Name);
                            //после замены textPos сбивается, нужно заново найти его значение
                            textPos = FragmentBody[k].IndexOf("\r\n\r\n");
                        }
                        //убрать лишние символы - пробелы, \r, \n
                        StringBuilder sbText = new StringBuilder(FragmentToReplace[k], FragmentToReplace[k].Length);
                        sbText.Replace("\r\n", String.Empty);
                        sbText.Replace(" ", String.Empty);
                        FragmentToReplace[k] = sbText.ToString();
                        Byte[] binaryData = Convert.FromBase64String(FragmentToReplace[k]);
                        //save image, file, audio, video
                        try
                        {
                            File.WriteAllBytes(Name, binaryData);
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine(e.ToString());
                            Console.ReadLine();
                        }
                        FragmentToReplace[k] = "Attachment: " + Name + " has been saved in 'POP3ClientAsync' folder";
                    }
                    FragmentBody[k] = FragmentBody[k].Substring(0, textPos + 4) + FragmentToReplace[k] + "\r\n";
                }
                newcontent = newcontent + boundstring + "\r\n" + FragmentBody[k];
            }
            if (ind == 0) ind = 1;
            return body = body.Substring(0, ind - 1) + newcontent + boundstring + "\r\n";
        }

        // Метод декодирования всех найденных совпадений
        private string HeadersEncode(Match m)
        {
            string result = String.Empty;
            Encoding cp = Encoding.GetEncoding(m.Groups["cp"].Value);
            if (m.Groups["ct"].Value.ToUpper() == "Q")
                // кодируем из Quoted-Printable
                result = ParseQuotedPrintable(m.Groups["value"].Value, m.Groups["cp"].Value);
            else if (m.Groups["ct"].Value.ToUpper() == "B")
                // кодируем из Base64
                result = cp.GetString(Convert.FromBase64String(m.Groups["value"].Value));
            else
                // оставляем текст как есть
                result = m.Groups["value"].Value;
            return result;
        }

        // Метод парсинга Quoted-Printable
        private string ParseQuotedPrintable(string source, string encode)
        {
            //удаляет знаки = в конце строк text/html
            source = Regex.Replace(source, @"(\=)([^\dABCDEF]{2})", "");
            //заменяет =3D на = в строках text/html
            source = Regex.Replace(source, @"[\=]{1}[3]{1}[D]{1}", "=");
            //вызов декодеров
            if (encode.ToLower() == "iso-8859-5" || encode.ToLower() == "koi8-r")
            {
                Regex regChar = new Regex(@"\=(?<char>[\dABCDEF]{2})");
                MatchCollection mc = regChar.Matches(source);
                foreach (Match m in mc)
                    if (m.Success)
                        source = regChar.Replace(source, QuotedPrintableEncode1b(m, encode), 1);
                return source;
            }
            else if (encode.ToLower() == "utf-8") //Unicode (UTF-8) 
                return Regex.Replace(source, @"(\=(?<char1>[\dABCDEF]{2})\=(?<char2>[\dABCDEF]{2})){1}", QuotedPrintableEncode2b);
            else
                return source;
        }

        //Метод декодирования Quoted-Printable 1 байтовый
        private string QuotedPrintableEncode1b(Match m, string encode)
        {
            byte[] bytes = new byte[1];
            //32-битное представление значения ["char"].Value
            int iHex = Convert.ToInt32(m.Groups["char"].Value, 16);
            bytes[0] = Convert.ToByte(iHex);
            return Encoding.GetEncoding(encode).GetString(bytes); //Cyrillic (ISO) or (KOI8-R)
        }

        //Метод декодирования Quoted-Printable 2 байтовый
        private string QuotedPrintableEncode2b(Match m)
        {
            byte[] bytes = new byte[2];
            //32-битное представление значения ["char"].Value
            int iHex1 = Convert.ToInt32(m.Groups["char1"].Value, 16);
            int iHex2 = Convert.ToInt32(m.Groups["char2"].Value, 16);
            bytes[0] = Convert.ToByte(iHex1);
            bytes[1] = Convert.ToByte(iHex2);
            return Encoding.GetEncoding(65001).GetString(bytes); //Unicode (UTF-8) 
        }

        // Метод декодирования содержимого письма
        private string DecodeContent(string contentTransferEncoding, string source, string encode)
        {
            if (contentTransferEncoding == "base64")
                return Encoding.GetEncoding(encode).GetString(Convert.FromBase64String(source));
            else if (contentTransferEncoding == "quoted-printable")
                return ParseQuotedPrintable(source, encode);
            else //"8bit", "7bit", "binary" 
                return source; // считаем, что это обычный текст
        }
    }
}
