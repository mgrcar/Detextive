using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using Latino;
using Latino.TextMining;
using Latino.Model;
using PosTagger;

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
        public static int TOP_ITEMS_COUNT
            = Convert.ToInt32(Utils.GetConfigValue("VectorItemsListSize", "100"));
        public static string OUTPUT_FILE
            = Utils.GetConfigValue("OutputFile", "output.tsv.txt");

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
<div class='container'>");
        }

        static void WriteFooter(StreamWriter w)
        {
            w.WriteLine(
@"</div>
<script src='jquery.js'></script>
<script src='bootstrap.min.js'></script>
<script src='jquery.tablesorter.min.js'></script>
<script src='code.js'></script>
</body>
</html>");
        }

        static Dictionary<string, string> mTranslations
            = new Dictionary<string, string>() {
                { "ttr", "Delež različnih besed" },
                { "brunet", "Brunétov indeks" },
                { "honore", "Honoréjeva statistika" },
                { "hl", "Hapax legomena" },
                { "ttrLemma", "Delež različnih lem" },
                { "brunetLemma", "Brunétov indeks (leme)" },
                { "honoreLemma", "Honoréjeva statistika (leme)" },
                { "hlLemma", "Hapax legomena (leme)" },
                { "rWords", "Razmerje med št. besed in št. povedi" },
                { "rChars", "Razmerje med št. znakov in št. besed	" },
                { "rSyllables", "Razmerje med št. zlogov in št. besed" },
                { "rComplex", "Delež kompleksnih besed" },
                { "ari", "ARI" },
                { "flesch", "Flesch" },
                { "fog", "Fog" }
            };

        static string Translate(string str)
        {
            if (mTranslations.ContainsKey(str)) { return mTranslations[str]; }
            return str;
        }

        static void WriteFeature(StreamWriter w, string name, double val)
        {
            w.WriteLine("<tr><td>{0}</td><td>{1:0.00}</td></tr>", HttpUtility.HtmlEncode(Translate(name)), val);
        }

        static void WriteFeature(StreamWriter w, string name, double val, double stdDev)
        {
            WriteFeature(w, name, val, stdDev, /*sameCell=*/false);
        }

        static void WriteFeature(StreamWriter w, string name, double val, double stdDev, bool sameCell)
        {
            if (!sameCell)
            {
                w.WriteLine("<tr><td>{0}</td><td>{1:0.00}</td><td>±&nbsp;{2:0.00}</td></tr>", HttpUtility.HtmlEncode(Translate(name)), val, stdDev);
            }
            else
            {
                w.WriteLine("<tr><td>{0}</td><td>{1:0.00}&nbsp;±&nbsp;{2:0.00}</td></tr>", HttpUtility.HtmlEncode(Translate(name)), val, stdDev);
            }
        }

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

        private static void WriteAuthorCompareTable(StreamWriter writer, IEnumerable<Author> authors, Author author, IEnumerable<string> featureNames, bool isVec)
        {
            foreach (Author otherAuthor in authors)
            {
                if (author != otherAuthor)
                {
                    if (!isVec || !otherAuthor.mIsTagged)
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
                bool isTaggedAuthor = false;
                if (authorName.EndsWith(".tag", StringComparison.OrdinalIgnoreCase))
                {
                    authorName = authorName.Substring(0, authorName.Length - 4);
                    isTaggedAuthor = true;
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
                    text.mIsTagged = isTaggedAuthor;
                    Author author;
                    if (!authors.TryGetValue(text.mAuthor, out author))
                    {
                        author = new Author(text.mAuthor);
                        author.mIsTagged = isTaggedAuthor;
                        author.mTexts.Add(text);
                        authors.Add(text.mAuthor, author);
                    }
                    else { author.mTexts.Add(text); }
                }
            }
            FunctionWordsModel fuw = new FunctionWordsModel();
            fuw.Initialize(authors.Values);
            FrequentWordsModel frw = new FrequentWordsModel();
            frw.Initialize(authors.Values);
            FrequentLemmasModel frl = new FrequentLemmasModel();
            frl.Initialize(authors.Values);
            CharNGramsModel cng = new CharNGramsModel();
            cng.Initialize(authors.Values);
            PosTagsModel pos = new PosTagsModel();
            pos.Initialize(authors.Values);
            string[] vecNames = new string[] { "fuw", "frw", "frl", "cng", "pos" };
            ModelBase[] modelBs = new ModelBase[] { fuw, frw, frl, cng, pos };
            foreach (Author author in authors.Values)
            {
                author.ComputeFeatures();
                for (int i = 0; i < vecNames.Length; i++)
                {
                    author.mPredictions.Add(vecNames[i], modelBs[i].mModels[author.mName].Predict(author.mFeatureVectors[vecNames[i]]));
                }
            }

            ArrayList<Author> authorsArray = new ArrayList<Author>(authors.Values);
            foreach (Author author in authorsArray)
            {
                foreach (string feature in new ArrayList<string>(author.mFeatures.Keys))
                {
                    Features.GetFeatureRanking(author, authorsArray, feature);
                }
            }


            // write results
            logger.Info("Main", "Pišem rezultate ...");
            using (StreamWriter wTsv = new StreamWriter(OUTPUT_FILE, /*append=*/false, Encoding.UTF8))
            {
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
                        if (author.mIsTagged)
                        {
                            wIdx.WriteLine("<div class='alert alert-info'><strong>Neznani avtor.</strong> <a href='{0}'>Primerjaj z ostalimi avtorji »</a></div>", "compare_" + authorNum + ".html");
                        }
                        else
                        {
                            wIdx.WriteLine("<p><a href='{0}'>Primerjaj z ostalimi avtorji »</a></p>", "compare_" + authorNum + ".html");
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
                                foreach (string featureNm in "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma".Split(','))
                                {
                                    WriteFeature(wDoc, featureNm, text.mFeatures[featureNm]);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("<h2>Berljivost</h2>");
                                wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                                wDoc.WriteLine("<thead>");
                                wDoc.WriteLine("<tr><th>Značilka</th><th>Vrednost</th></tr>");
                                wDoc.WriteLine("</thead>");
                                wDoc.WriteLine("<tbody>");
                                foreach (string featureNm in "rWords,rChars,rSyllables,rComplex,ari,flesch,fog".Split(','))
                                {
                                    WriteFeature(wDoc, featureNm, text.mFeatures[featureNm]);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("<h2>Funkcijske besede</h2>");
                                wDoc.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#fuw'>Seznam funkcijskih besed</a></p>");
                                wDoc.WriteLine("<div id='fuw' class='collapse'>");
                                wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                                wDoc.WriteLine("<thead>");
                                wDoc.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                                wDoc.WriteLine("</thead>");
                                wDoc.WriteLine("<tbody>");
                                int i = 0;
                                foreach (KeyDat<double, Word> wordInfo in fuw.mBowSpace.GetKeywords(text.mFeatureVectors["fuw"]).Take(TOP_ITEMS_COUNT))
                                {
                                    wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("</div>");
                                wDoc.WriteLine("<h2>Pogoste besede</h2>");
                                wDoc.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#frw'>Seznam pogostih besed</a></p>");
                                wDoc.WriteLine("<div id='frw' class='collapse'>");
                                wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                                wDoc.WriteLine("<thead>");
                                wDoc.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                                wDoc.WriteLine("</thead>");
                                wDoc.WriteLine("<tbody>");
                                i = 0;
                                foreach (KeyDat<double, Word> wordInfo in frw.mBowSpace.GetKeywords(text.mFeatureVectors["frw"]).Take(TOP_ITEMS_COUNT))
                                {
                                    wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("</div>");
                                wDoc.WriteLine("<h2>Pogoste leme</h2>");
                                wDoc.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#frl'>Seznam pogostih lem</a></p>");
                                wDoc.WriteLine("<div id='frl' class='collapse'>");
                                wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                                wDoc.WriteLine("<thead>");
                                wDoc.WriteLine("<tr><th>Zap. št.</th><th>Lema</th><th>Utež</th></tr>");
                                wDoc.WriteLine("</thead>");
                                wDoc.WriteLine("<tbody>");
                                i = 0;
                                foreach (KeyDat<double, Word> wordInfo in frl.mBowSpace.GetKeywords(text.mFeatureVectors["frl"]).Take(TOP_ITEMS_COUNT))
                                {
                                    wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("</div>");

                                wDoc.WriteLine("<h2>Znakovna zaporedja</h2>");
                                wDoc.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#cng'>Seznam znakovnih zaporedij</a></p>");
                                wDoc.WriteLine("<div id='cng' class='collapse'>");
                                wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                                wDoc.WriteLine("<thead>");
                                wDoc.WriteLine("<tr><th>Zap. št.</th><th>Zaporedje</th><th>Utež</th></tr>");
                                wDoc.WriteLine("</thead>");
                                wDoc.WriteLine("<tbody>");
                                i = 0;
                                foreach (KeyDat<double, Word> wordInfo in cng.mBowSpace.GetKeywords(text.mFeatureVectors["cng"]).Take(TOP_ITEMS_COUNT))
                                {
                                    wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("</div>");

                                wDoc.WriteLine("<h2>Oblikoslovne oznake</h2>");
                                wDoc.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#pos'>Seznam oblikoslovnih oznak</a></p>");
                                wDoc.WriteLine("<div id='pos' class='collapse'>");
                                wDoc.WriteLine("<table class='table table-bordered table-striped'>");
                                wDoc.WriteLine("<thead>");
                                wDoc.WriteLine("<tr><th>Zap. št.</th><th>Zaporedje</th><th>Utež</th></tr>");
                                wDoc.WriteLine("</thead>");
                                wDoc.WriteLine("<tbody>");
                                i = 0;
                                foreach (KeyDat<double, Word> wordInfo in pos.mBowSpace.GetKeywords(text.mFeatureVectors["pos"]).Take(TOP_ITEMS_COUNT))
                                {
                                    wDoc.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++i, HttpUtility.HtmlEncode(wordInfo.Dat.Stem), wordInfo.Key);
                                }
                                wDoc.WriteLine("</tbody>");
                                wDoc.WriteLine("</table>");
                                wDoc.WriteLine("</div>");

                                WriteFooter(wDoc);
                            }
                        }
                        wIdx.WriteLine("</ul>");
                        wIdx.WriteLine("<h3>Značilke</h3>");
                        wIdx.WriteLine("<h4>Obseg besedišča</h4>");
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Značilka</th><th>Vrednost</th><th>Std. odklon</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        WriteFeature(wIdx, "Delež različnih besed", author.GetAvg("ttr"), author.GetStdDev("ttr"));
                        WriteFeature(wIdx, "Brunétov indeks", author.GetAvg("brunet"), author.GetStdDev("brunet"));
                        WriteFeature(wIdx, "Honoréjeva statistika", author.GetAvg("honore"), author.GetStdDev("honore"));
                        WriteFeature(wIdx, "Hapax legomena", author.GetAvg("hl"), author.GetStdDev("hl"));
                        WriteFeature(wIdx, "Delež različnih lem", author.GetAvg("ttrLemma"), author.GetStdDev("ttrLemma"));
                        WriteFeature(wIdx, "Brunétov indeks (leme)", author.GetAvg("brunetLemma"), author.GetStdDev("brunetLemma"));
                        WriteFeature(wIdx, "Honoréjeva statistika (leme)", author.GetAvg("honoreLemma"), author.GetStdDev("honoreLemma"));
                        WriteFeature(wIdx, "Hapax legomena (leme)", author.GetAvg("hlLemma"), author.GetStdDev("hlLemma"));
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");
                        wIdx.WriteLine("<h4>Berljivost</h4>");
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Značilka</th><th>Vrednost</th><th>Std. odklon</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        WriteFeature(wIdx, "Razmerje med št. besed in št. povedi", author.GetAvg("rWords"), author.GetStdDev("rWords"));
                        WriteFeature(wIdx, "Razmerje med št. znakov in št. besed", author.GetAvg("rChars"), author.GetStdDev("rChars"));
                        WriteFeature(wIdx, "Razmerje med št. zlogov in št. besed", author.GetAvg("rSyllables"), author.GetStdDev("rSyllables"));
                        WriteFeature(wIdx, "Delež kompleksnih besed", author.GetAvg("rComplex"), author.GetStdDev("rComplex"));
                        WriteFeature(wIdx, "ARI", author.GetAvg("ari"), author.GetStdDev("ari"));
                        WriteFeature(wIdx, "Flesch", author.GetAvg("flesch"), author.GetStdDev("flesch"));
                        WriteFeature(wIdx, "Fog", author.GetAvg("fog"), author.GetStdDev("fog"));
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");

                        wIdx.WriteLine("<h4>Razlikovalna moč značilk</h4>");
                        wIdx.WriteLine("<table class='tablesorter table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Značilka</th><th>Utež</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        foreach (string pKey in author.mFeatures.Keys.Where(x => x.StartsWith("p_")))
                        {
                            WriteFeature(wIdx, pKey.Substring(2), author.GetAvg(pKey), author.GetStdDev(pKey), /*sameCell=*/true);
                        }
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");

                        wIdx.WriteLine("<h4>Funkcijske besede</h4>");
                        wIdx.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#fuw_{0}'>Seznam funkcijskih besed</a></p>", authorNum);
                        wIdx.WriteLine("<div id='fuw_{0}' class='collapse'>", authorNum);
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        int j = 0;
                        foreach (Pair<string, double> word in author.GetTopVectorItems("fuw", TOP_ITEMS_COUNT, fuw.mBowSpace))
                        {
                            wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                        }
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");
                        wIdx.WriteLine("</div>");
                        wIdx.WriteLine("<h4>Pogoste besede</h4>");
                        wIdx.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#frw_{0}'>Seznam pogostih besed</a></p>", authorNum);
                        wIdx.WriteLine("<div id='frw_{0}' class='collapse'>", authorNum);
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        j = 0;
                        foreach (Pair<string, double> word in author.GetTopVectorItems("frw", TOP_ITEMS_COUNT, frw.mBowSpace))
                        {
                            wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                        }
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");
                        wIdx.WriteLine("</div>");
                        wIdx.WriteLine("<h4>Pogoste leme</h4>");
                        wIdx.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#frl_{0}'>Seznam pogostih lem</a></p>", authorNum);
                        wIdx.WriteLine("<div id='frl_{0}' class='collapse'>", authorNum);
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Zap. št.</th><th>Beseda</th><th>Utež</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        j = 0;
                        foreach (Pair<string, double> word in author.GetTopVectorItems("frl", TOP_ITEMS_COUNT, frl.mBowSpace))
                        {
                            wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                        }
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");
                        wIdx.WriteLine("</div>");
                        wIdx.WriteLine("<h4>Znakovna zaporedja</h4>");
                        wIdx.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#cng_{0}'>Seznam znakovnih zaporedij</a></p>", authorNum);
                        wIdx.WriteLine("<div id='cng_{0}' class='collapse'>", authorNum);
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Zap. št.</th><th>Zaporedje</th><th>Utež</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        j = 0;
                        foreach (Pair<string, double> word in author.GetTopVectorItems("cng", TOP_ITEMS_COUNT, cng.mBowSpace))
                        {
                            wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                        }
                        wIdx.WriteLine("</tbody>");
                        wIdx.WriteLine("</table>");
                        wIdx.WriteLine("</table>");
                        wIdx.WriteLine("</div>");
                        wIdx.WriteLine("<h4>Oblikoslovne oznake</h4>");
                        wIdx.WriteLine("<p><a href='javascript:void(0)' data-toggle='collapse' data-target='#pos_{0}'>Seznam oblikoslovnih oznak</a></p>", authorNum);
                        wIdx.WriteLine("<div id='pos_{0}' class='collapse'>", authorNum);
                        wIdx.WriteLine("<table class='table table-bordered table-striped'>");
                        wIdx.WriteLine("<thead>");
                        wIdx.WriteLine("<tr><th>Zap. št.</th><th>Zaporedje</th><th>Utež</th></tr>");
                        wIdx.WriteLine("</thead>");
                        wIdx.WriteLine("<tbody>");
                        j = 0;
                        foreach (Pair<string, double> word in author.GetTopVectorItems("pos", TOP_ITEMS_COUNT, pos.mBowSpace))
                        {
                            wIdx.WriteLine("<tr><td>{0}.</td><td>{1}</td><td>{2:0.00}</td></tr>", ++j, HttpUtility.HtmlEncode(word.First), word.Second);
                        }
                        wIdx.WriteLine("</tbody>");
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
                        wAuthorCmp.WriteLine("<tr><th>Avtor</th><th>DRB</th><th>BI</th><th>HS</th><th>HL</th><th>DRL</th><th>BI-L</th><th>HS-L</th><th>HL-L</th></tr>");
                        wAuthorCmp.WriteLine("</thead>");
                        wAuthorCmp.WriteLine("<tbody>");
                        WriteAuthorCompareTable(wAuthorCmp, authors.Values, author, "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma".Split(','), /*isVec=*/false);
                        wAuthorCmp.WriteLine("</tbody>");
                        wAuthorCmp.WriteLine("</table>");
                        wAuthorCmp.WriteLine("<h3>Berljivost</h3>");
                        wAuthorCmp.WriteLine("<table class='tablesorter table table-bordered table-striped'>");
                        wAuthorCmp.WriteLine("<thead>");
                        wAuthorCmp.WriteLine("<tr><th>Avtor</th><th>B/P</th><th>Zn./B</th><th>Zl./B</th><th>DKB</th><th>ARI</th><th>Flesch</th><th>Fog</th></tr>");
                        wAuthorCmp.WriteLine("</thead>");
                        wAuthorCmp.WriteLine("<tbody>");
                        WriteAuthorCompareTable(wAuthorCmp, authors.Values, author, "rWords,rChars,rSyllables,rComplex,ari,flesch,fog".Split(','), /*isVec=*/false);
                        wAuthorCmp.WriteLine("</tbody>");
                        wAuthorCmp.WriteLine("</table>");
                        wAuthorCmp.WriteLine("<h3>Vektorji značilk</h3>");
                        wAuthorCmp.WriteLine("<table class='tablesorter table table-bordered table-striped'>");
                        wAuthorCmp.WriteLine("<thead>");
                        wAuthorCmp.WriteLine("<tr><th>Avtor</th><th>FB</th><th>PB</th><th>PL</th><th>ZZ</th><th>Ozn.</th></tr>");
                        wAuthorCmp.WriteLine("</thead>");
                        wAuthorCmp.WriteLine("<tbody>");
                        WriteAuthorCompareTable(wAuthorCmp, authors.Values, author, "fuw,frw,frl,cng,pos".Split(','), /*isVec=*/true);
                        wAuthorCmp.WriteLine("</tbody>");
                        wAuthorCmp.WriteLine("</table>");
                        WriteFooter(wAuthorCmp);
                    }
                }
            }
        }
    }
}
