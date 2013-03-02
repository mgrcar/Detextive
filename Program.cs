﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using Latino;
using PosTagger;
using Latino.TextMining;
using Latino.Model;

namespace Detextive
{
    static class Program
    {
        static string POS_TAGGER_MODEL
            = Utils.GetConfigValue("PosTaggerModel", "TaggerFeb2012.bin");
        static string LEMMATIZER_MODEL
            = Utils.GetConfigValue("LemmatizerModel", "LemmatizerFeb2012.bin");
        static string DATA_FOLDER
            = Utils.GetConfigValue("DataFolder", ".").TrimEnd('\\');
        static string DATA_ENCODING
            = Utils.GetConfigValue("DataEncoding", "UTF-8");
        static string OUTPUT_PATH
            = Utils.GetConfigValue("OutputPath", ".").TrimEnd('\\');
        //static int NUM_TOP_TOKENS
        //    = 100;
        //static int NUM_TOP_LEMMAS
        //    = 100;

        static void WriteHeader(StreamWriter w)
        {
            w.WriteLine(
@"<!DOCTYPE html>
<html>
<head>
<meta http-equiv='Content-Type' content='text/html; charset=utf-8'>
<link rel='stylesheet' type='text/css' href='bootstrap.min.css'>
<link rel='stylesheet' type='text/css' href='styles.css'>
</head>
<body>
<div class='container'>
<div class='inner'>");
        }

        static void WriteFooter(StreamWriter w)
        {
            w.WriteLine(
@"</div>
</div>
<script src='jquery.js'></script>
<script src='bootstrap.min.js'></script>
<script src='jquery.tablesorter.min.js'></script>
<script src='code.js'></script>
</body>
</html>");
        }

        static void WriteFeature(StreamWriter w, string name, double val)
        {
            w.WriteLine("<tr><td>{0}</td><td>{1:0.00}</td></tr>", HttpUtility.HtmlEncode(name), val);
        }

        static void WriteFeature(StreamWriter w, string name, double val, double stdDev)
        {
            w.WriteLine("<tr><td>{0}</td><td>{1:0.00}</td><td>±&nbsp;{2:0.00}</td></tr>", HttpUtility.HtmlEncode(name), val, stdDev);
        }

        //static void CountTokens(Text text, MultiSet<string> tokens, MultiSet<string> lemmas)
        //{
        //    foreach (Sentence sentence in text.mSentences)
        //    {
        //        foreach (Token token in sentence.mTokens)
        //        {
        //            if (!token.mIsPunctuation)
        //            {
        //                tokens.Add(token.mTokenStr.ToLower());
        //                lemmas.Add(token.mLemma.ToLower());
        //            }
        //        }
        //    }
        //}

        public static double StdDev(this IEnumerable<double> values)
        {
            double ret = 0;
            int count = values.Count();
            if (count > 1)
            {
                double avg = values.Average();
                double sum = values.Sum(d => (d - avg) * (d - avg));
                ret = Math.Sqrt(sum / count);
            }
            return ret;
        }

        private static void WriteAuthorCompareTable(StreamWriter writer, IEnumerable<Author> authors, Author author, IEnumerable<string> featureNames)
        {
            foreach (Author otherAuthor in authors)
            {
                if (author != otherAuthor)
                {
                    Dictionary<string, double> diff, stdDev;
                    author.ComputeDistance(otherAuthor, out diff, out stdDev, featureNames);
                    writer.WriteLine("<tr>");
                    writer.WriteLine("<td>{0}</td>", otherAuthor.mName);
                    foreach (KeyValuePair<string, double> item in diff)
                    {
                        if (stdDev.ContainsKey(item.Key)) { writer.WriteLine("<td>{0:0.00}&nbsp;±&nbsp;{1:0.00}</td>", item.Value, stdDev[item.Key]); }
                        else { writer.WriteLine("<td>{0:0.00}</td>", item.Value); }
                    }
                    writer.WriteLine("</tr>");
                }
            }
        }

        static void Main(string[] args)
        {
            // setup logger
            Logger logger = Logger.GetRootLogger();
            logger.LocalLevel = Logger.Level.Debug;
            logger.LocalOutputType = Logger.OutputType.Custom;
            Logger.CustomOutput = 
                delegate(string loggerName, Logger.Level level, string funcName, Exception e, string message, object[] msgArgs) 
                {
                    Console.WriteLine(message, msgArgs);
                };
            // load POS tagger models
            logger.Info("Main", "Nalagam modele za oblikoslovno analizo ...");            
            PartOfSpeechTagger posTagger = new PartOfSpeechTagger(POS_TAGGER_MODEL, LEMMATIZER_MODEL);
            // load and preprocess texts
            MultiSet<string> tokens = new MultiSet<string>();
            MultiSet<string> lemmas = new MultiSet<string>();
            logger.Info("Main", "Nalagam podatke ...");
            Dictionary<string, Author> authors = new Dictionary<string, Author>();
            DirectoryInfo[] authorDirs = new DirectoryInfo(DATA_FOLDER).GetDirectories();//.Take(3).ToArray();
            foreach (DirectoryInfo authorDir in authorDirs)
            {
                string authorName = authorDir.Name;
                bool isTestAuthor = false;
                if (authorName.EndsWith(".test", StringComparison.OrdinalIgnoreCase)) 
                {
                    authorName = authorName.Substring(0, authorName.Length - 5);
                    isTestAuthor = true;
                }
                logger.Info("Main", "Obravnavam avtorja \"" + authorName + "\" ...");
                FileInfo[] authorFiles = authorDir.GetFiles("*.txt");
                foreach (FileInfo authorFile in authorFiles)
                {
                    string txt = File.ReadAllText(authorFile.FullName, Encoding.GetEncoding(DATA_ENCODING));
                    Match m = Regex.Match(txt, "^(.*?)(\r)?\n");
                    string title = m.Result("$1").Trim();
                    logger.Info("Main", "Obravnavam članek \"" + title + "\" ...");
                    // preprocess text
                    Corpus corpus = new Corpus();
                    corpus.LoadFromTextSsjTokenizer(txt);
                    posTagger.Tag(corpus);
                    Text text = new Text(corpus, title, authorName);
                    text.mIsTestText = isTestAuthor;
                    //CountTokens(text, tokens, lemmas);
                    Author author;
                    if (!authors.TryGetValue(text.mAuthor, out author))
                    {
                        author = new Author(text.mAuthor);
                        author.mIsTestAuthor = isTestAuthor;
                        author.mTexts.Add(text);
                        authors.Add(text.mAuthor, author);                        
                    }
                    else { author.mTexts.Add(text); }
                }
            }
            ArrayList<Text> texts = new ArrayList<Text>();
            foreach (Author author in authors.Values)
            {
                author.ComputeFeatures();
                texts.AddRange(author.mTexts);
            }
            FunctionWords.Initialize(texts);
            Set<string> test;
            FrequentWords.Initialize(texts, out test);
            FrequentLemmas.Initialize(texts);
            foreach (Author author in authors.Values)
            {
                author.ComputeCentroids();
            }
            //// get top tokens
            //Set<string> topTokens = new Set<string>(
            //    tokens.ToList()
            //    .OrderByDescending(x => x.Key)
            //    .Take(NUM_TOP_TOKENS)
            //    .Select(x => x.Dat));
            //// get top lemmas
            //Set<string> topLemmas = new Set<string>(
            //    lemmas.ToList()
            //    .OrderByDescending(x => x.Key)
            //    .Take(NUM_TOP_LEMMAS)
            //    .Select(x => x.Dat));
            //Console.WriteLine(Set<string>.Difference(test, topTokens));
            //Console.WriteLine(Set<string>.Difference(topTokens, test));
            // write results
            logger.Info("Main", "Pišem rezultate ...");
            using (StreamWriter wIdx = new StreamWriter(OUTPUT_PATH + "\\index.html", /*append=*/false, Encoding.UTF8))
            {
                WriteHeader(wIdx);
                wIdx.WriteLine("<h1>Rezultati analize</h1>");
                int authorNum = 0;
                foreach (KeyValuePair<string, Author> item in authors)
                {
                    authorNum++;
                    Author author = item.Value;
                    wIdx.WriteLine("<h2>Avtor: {0}</h2>", HttpUtility.HtmlEncode(item.Key));
                    if (author.mIsTestAuthor)
                    {
                        wIdx.WriteLine("<div class='alert alert-info'><strong>Neznani avtor.</strong> <a href='{0}'>Primerjaj z ostalimi avtorji »</a></div>", "compare_" + authorNum + ".html");
                    }
                    else
                    {
                        wIdx.WriteLine("<a href='{0}'>Primerjaj z ostalimi avtorji »</a>", "compare_" + authorNum + ".html"); 
                    }
                    wIdx.WriteLine("<h3>Besedila</h3>");
                    wIdx.WriteLine("<ul>");
                    foreach (Text text in item.Value.mTexts)
                    {
                        wIdx.WriteLine("<li><a href='{1}'>{0} »</a></li>", HttpUtility.HtmlEncode(text.mName), text.mHtmlFileName);
                        using (StreamWriter wDoc = new StreamWriter(OUTPUT_PATH + "\\" + text.mHtmlFileName, /*append=*/false, Encoding.UTF8))
                        {
                            // write document HTML
                            WriteHeader(wDoc);
                            wDoc.WriteLine("<div class='back'><a href='index.html'>« Seznam avtorjev</a></div>");
                            wDoc.WriteLine("<h1>Besedilo</h1>");
                            wDoc.WriteLine("<h2>{0}</h2>", HttpUtility.HtmlEncode(text.mName));
                            wDoc.WriteLine(text.GetHtml());
                            wDoc.WriteLine("<h1>Značilke</h1>");
                            wDoc.WriteLine("<h2>Obseg besedišča</h2>");
                            wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                            wDoc.WriteLine("<thead>");
                            wDoc.WriteLine("<tr><th>Značilka</th><th>Vrednost</th></tr>");
                            wDoc.WriteLine("</thead>");
                            wDoc.WriteLine("<tbody>");
                            WriteFeature(wDoc, "Delež različnih besed", text.mFeatures["ttr"]);
                            WriteFeature(wDoc, "Brunetov indeks", text.mFeatures["brunet"]);
                            WriteFeature(wDoc, "Honorejeva statistika", text.mFeatures["honore"]);
                            WriteFeature(wDoc, "Hapax legomena", text.mFeatures["hl"]);
                            wDoc.WriteLine("</tbody>");
                            wDoc.WriteLine("</table>");
                            wDoc.WriteLine("<h2>Berljivost</h2>");
                            wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                            wDoc.WriteLine("<tr><th>Značilka</th><th>Vrednost</th></tr>");
                            WriteFeature(wDoc, "Razmerje med št. besed in št. povedi", text.mFeatures["rWords"]);
                            WriteFeature(wDoc, "Razmerje med št. znakov in št. besed", text.mFeatures["rChars"]);
                            WriteFeature(wDoc, "Razmerje med št. zlogov in št. besed", text.mFeatures["rSyllables"]);
                            WriteFeature(wDoc, "Delež kompleksnih besed", text.mFeatures["rComplex"]);
                            WriteFeature(wDoc, "ARI", text.mFeatures["ari"]);
                            WriteFeature(wDoc, "Flesch", text.mFeatures["flesch"]);
                            WriteFeature(wDoc, "Fog", text.mFeatures["fog"]);
                            wDoc.WriteLine("</table>");
                            wDoc.WriteLine("<h2>Funkcijske besede</h2>");
                            wDoc.WriteLine("<a href='javascript:void(0)' data-toggle='collapse' data-target='#fuw'>Seznam funkcijskih besed</a>");
                            wDoc.WriteLine("<div id='fuw' class='collapse'>");
                            wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                            wDoc.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                            int i = 0;
                            foreach (KeyDat<double, Word> wordInfo in FunctionWords.mBowSpace.GetKeywords(text.mFeatureVectors["fuw"]))
                            {
                                wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                            }
                            wDoc.WriteLine("</table>");
                            wDoc.WriteLine("</div>");
                            wDoc.WriteLine("<h2>Pogoste besede</h2>");
                            wDoc.WriteLine("<a href='javascript:void(0)' data-toggle='collapse' data-target='#frw'>Seznam pogostih besed</a>");
                            wDoc.WriteLine("<div id='frw' class='collapse'>");
                            wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                            wDoc.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                            i = 0;
                            foreach (KeyDat<double, Word> wordInfo in FrequentWords.mBowSpace.GetKeywords(text.mFeatureVectors["frw"]))
                            {
                                wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                            }
                            wDoc.WriteLine("</table>");
                            wDoc.WriteLine("</div>");
                            wDoc.WriteLine("<h2>Pogoste leme</h2>");
                            wDoc.WriteLine("<a href='javascript:void(0)' data-toggle='collapse' data-target='#frl'>Seznam pogostih lem</a>");
                            wDoc.WriteLine("<div id='frl' class='collapse'>");
                            wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                            wDoc.WriteLine("<tr><th>Zap. št.</th><th>Lema</th><th>Utež</th></tr>");
                            i = 0;
                            foreach (KeyDat<double, Word> wordInfo in FrequentLemmas.mBowSpace.GetKeywords(text.mFeatureVectors["frl"]))
                            {
                                wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                            }
                            wDoc.WriteLine("</table>");
                            wDoc.WriteLine("</div>");
                            WriteFooter(wDoc);
                        }
                    }
                    wIdx.WriteLine("</ul>");
                    wIdx.WriteLine("<h3>Značilke</h3>");
                    wIdx.WriteLine("<h4>Obseg besedišča</h4>");
                    wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                    wIdx.WriteLine("<tr><th>Značilka</th><th>Vrednost</th><th>Std. odklon</th></tr>");
                    WriteFeature(wIdx, "Delež različnih besed", author.GetAvg("ttr"), author.GetStdDev("ttr"));
                    WriteFeature(wIdx, "Brunetov indeks", author.GetAvg("brunet"), author.GetStdDev("brunet"));
                    WriteFeature(wIdx, "Honorejeva statistika", author.GetAvg("honore"), author.GetStdDev("honore"));
                    WriteFeature(wIdx, "Hapax legomena", author.GetAvg("hl"), author.GetStdDev("hl"));
                    wIdx.WriteLine("</table>");
                    wIdx.WriteLine("<h4>Berljivost</h4>");
                    wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                    wIdx.WriteLine("<tr><th>Značilka</th><th>Vrednost</th><th>Std. odklon</th></tr>");
                    WriteFeature(wIdx, "Razmerje med št. besed in št. povedi", author.GetAvg("rWords"), author.GetStdDev("rWords"));
                    WriteFeature(wIdx, "Razmerje med št. znakov in št. besed", author.GetAvg("rChars"), author.GetStdDev("rChars"));
                    WriteFeature(wIdx, "Razmerje med št. zlogov in št. besed", author.GetAvg("rSyllables"), author.GetStdDev("rSyllables"));
                    WriteFeature(wIdx, "Delež kompleksnih besed", author.GetAvg("rComplex"), author.GetStdDev("rComplex"));
                    WriteFeature(wIdx, "ARI", author.GetAvg("ari"), author.GetStdDev("ari"));
                    WriteFeature(wIdx, "Flesch", author.GetAvg("flesch"), author.GetStdDev("flesch"));
                    WriteFeature(wIdx, "Fog", author.GetAvg("fog"), author.GetStdDev("fog"));
                    wIdx.WriteLine("</table>");
                    wIdx.WriteLine("<h4>Funkcijske besede</h4>");
                    wIdx.WriteLine("<a href='javascript:void(0)' data-toggle='collapse' data-target='#fuw_{0}'>Seznam funkcijskih besed</a>", authorNum);
                    wIdx.WriteLine("<div id='fuw_{0}' class='collapse'>", authorNum);
                    wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                    wIdx.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                    int j = 0;
                    foreach (Pair<string, double> word in author.GetTopVectorItems("fuw", int.MaxValue, FunctionWords.mBowSpace))
                    {
                        wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                    }
                    wIdx.WriteLine("</table>");
                    wIdx.WriteLine("</div>");
                    wIdx.WriteLine("<h4>Pogoste besede</h4>");
                    wIdx.WriteLine("<a href='javascript:void(0)' data-toggle='collapse' data-target='#frw_{0}'>Seznam pogostih besed</a>", authorNum);
                    wIdx.WriteLine("<div id='frw_{0}' class='collapse'>", authorNum);
                    wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                    wIdx.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                    j = 0;
                    foreach (Pair<string, double> word in author.GetTopVectorItems("frw", int.MaxValue, FrequentWords.mBowSpace))
                    {
                        wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                    }
                    wIdx.WriteLine("</table>");
                    wIdx.WriteLine("</div>");
                    wIdx.WriteLine("<h4>Pogoste leme</h4>");
                    wIdx.WriteLine("<a href='javascript:void(0)' data-toggle='collapse' data-target='#frl_{0}'>Seznam pogostih lem</a>", authorNum);
                    wIdx.WriteLine("<div id='frl_{0}' class='collapse'>", authorNum);
                    wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                    wIdx.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                    j = 0;
                    foreach (Pair<string, double> word in author.GetTopVectorItems("frl", int.MaxValue, FrequentLemmas.mBowSpace))
                    {
                        wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                    }
                    wIdx.WriteLine("</table>");
                    wIdx.WriteLine("</div>");
                }
                WriteFooter(wIdx);  
            }
            // write author-compare pages
            int n = 0;
            foreach (Author author in authors.Values)
            {
                string authorCompareFileName = OUTPUT_PATH + "\\compare_" + ++n + ".html";
                using (StreamWriter wAuthorCmp = new StreamWriter(authorCompareFileName, /*append=*/false, Encoding.UTF8))
                {
                    WriteHeader(wAuthorCmp);
                    wAuthorCmp.WriteLine("<div class='back'><a href='index.html'>« Seznam avtorjev</a></div>");
                    wAuthorCmp.WriteLine("<h1>Primerjava</h1>");
                    wAuthorCmp.WriteLine("<h2>Avtor: {0}</h2>", HttpUtility.HtmlEncode(author.mName));
                    wAuthorCmp.WriteLine("<h3>Obseg besedišča</h3>");
                    wAuthorCmp.WriteLine("<table class='tablesorter table table-bordered table-striped'>");
                    wAuthorCmp.WriteLine("<thead>");
                    wAuthorCmp.WriteLine("<tr><th>Avtor</th><th>DRB</th><th>BI</th><th>HS</th><th>HL</th></tr>");
                    wAuthorCmp.WriteLine("</thead>");
                    wAuthorCmp.WriteLine("<tbody>");
                    WriteAuthorCompareTable(wAuthorCmp, authors.Values, author, "ttr,brunet,honore,hl".Split(','));
                    wAuthorCmp.WriteLine("</tbody>");
                    wAuthorCmp.WriteLine("</table>");
                    wAuthorCmp.WriteLine("<h3>Berljivost</h3>");
                    wAuthorCmp.WriteLine("<table class='tablesorter table table-bordered table-striped'>");
                    wAuthorCmp.WriteLine("<thead>");
                    wAuthorCmp.WriteLine("<tr><th>Avtor</th><th>B/P</th><th>Zn/B</th><th>Zl/B</th><th>DKB</th><th>ARI</th><th>Flesch</th><th>Fog</th></tr>");
                    wAuthorCmp.WriteLine("</thead>");
                    wAuthorCmp.WriteLine("<tbody>");
                    WriteAuthorCompareTable(wAuthorCmp, authors.Values, author, "rWords,rChars,rSyllables,rComplex,ari,flesch,fog".Split(','));
                    wAuthorCmp.WriteLine("</tbody>");
                    wAuthorCmp.WriteLine("</table>");
                    //wAuthorCmp.WriteLine("<h3>Vektorji značilk</h3>");
                    //wAuthorCmp.WriteLine("<table class='tablesorter table table-bordered table-striped'>");
                    //wAuthorCmp.WriteLine("<thead>");
                    //wAuthorCmp.WriteLine("<tr><th>Avtor</th><th>FB</th><th>PB</th><th>PL</th></tr>");
                    //wAuthorCmp.WriteLine("</thead>");
                    //wAuthorCmp.WriteLine("<tbody>");
                    //WriteAuthorCompareTable(wAuthorCmp, authors.Values, author, "fuw,frw,frl".Split(','));
                    //wAuthorCmp.WriteLine("</tbody>");
                    //wAuthorCmp.WriteLine("</table>");
                    WriteFooter(wAuthorCmp);
                }
            }
        }
    }
}
