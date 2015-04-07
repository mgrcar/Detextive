using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Collections.Generic;
using Latino;
using PosTagger;
using Latino.Model;

namespace Detextive
{
    public class Token
    {
        public string mTokenStr;
        public string mLemma;
        public string mTag;
        public bool mIsPunctuation
            = false;
        public bool mIsFollowedBySpace
            = false;
    }

    public class Sentence
    {
        public ArrayList<Token> mTokens
            = new ArrayList<Token>();
    }    

    public class Author
    {
        public class FeatureMapping
        {
            public Dictionary<string, int> mTokenToIdx
                = new Dictionary<string,int>();
            public ArrayList<string> mIdxToToken
                = new ArrayList<string>();

            public int GetIdx(string token)
            {
                int idx;
                if (!mTokenToIdx.TryGetValue(token, out idx))
                {
                    mTokenToIdx.Add(token, idx = mTokenToIdx.Count);
                    mIdxToToken.Add(token);
                }
                return idx;
            }

            public string GetToken(int idx)
            {
                return mIdxToToken[idx];
            }
        }

        public string mName;
        public ArrayList<Text> mTexts
            = new ArrayList<Text>();
        public Dictionary<string, ArrayList<double>> mFeatures
            = new Dictionary<string, ArrayList<double>>();
        public Dictionary<string, SparseVector<double>> mFeatureVectors
            = new Dictionary<string, SparseVector<double>>();
        public static Dictionary<string, FeatureMapping> mFeatureMappings
            = new Dictionary<string, FeatureMapping>();

        public Author(string name)
        {
            mName = name;
        }

        public void AddFeatureVal(string featureName, double val)
        {
            ArrayList<double> values;
            if (!mFeatures.TryGetValue(featureName, out values))
            {
                mFeatures.Add(featureName, new ArrayList<double>(new double[] { val }));
            }
            else
            {
                values.Add(val);
            }
        }

        private SparseVector<double> GetSparseVector(string vectorName, ArrayList<KeyDat<int, string>> vec)
        {
            FeatureMapping mapping;
            if (!mFeatureMappings.TryGetValue(vectorName, out mapping))
            {
                mFeatureMappings.Add(vectorName, mapping = new FeatureMapping());
            }
            SparseVector<double> sparseVec = new SparseVector<double>();
            foreach (KeyDat<int, string> item in vec)
            {
                sparseVec[mapping.GetIdx(item.Dat)] = item.Key;
            }
            ModelUtils.NrmVecL2(sparseVec);
            return sparseVec;
        }

        public Pair<string, double>[] GetTopVectorItems(string vectorName, int n)
        { 
            SparseVector<double> vec = mFeatureVectors[vectorName];
            FeatureMapping mapping = mFeatureMappings[vectorName];
            return vec
                .OrderByDescending(x => x.Dat)
                .Take(n)
                .Select(x => new Pair<string, double>(mapping.GetToken(x.Idx), x.Dat))
                .ToArray();
        }

        public void AddFeatureVector(string vectorName, ArrayList<KeyDat<int, string>> vec)
        {
            SparseVector<double> currentVec;
            if (!mFeatureVectors.TryGetValue(vectorName, out currentVec))
            {
                mFeatureVectors.Add(vectorName, GetSparseVector(vectorName, vec));
            }
            else
            {
                mFeatureVectors[vectorName] 
                    = ModelUtils.ComputeCentroid(new SparseVector<double>[] { currentVec, GetSparseVector(vectorName, vec) }, CentroidType.Sum);
            }
        }

        public void NormalizeFeatureVectors()
        {
            foreach (SparseVector<double> vec in mFeatureVectors.Values)
            {
                ModelUtils.NrmVecL2(vec);
            }
        }

        public double GetAvg(string featureName)
        {
            return mFeatures[featureName].Average();
        }

        public double GetStdDev(string featureName)
        {
            return mFeatures[featureName].StdDev();
        }

        public void ComputeDistance(Author otherAuthor, out Dictionary<string, double> diff, out Dictionary<string, double> stdDev, IEnumerable<string> featureNames)
        {
            diff = new Dictionary<string, double>();
            stdDev = new Dictionary<string, double>();
            foreach (string featureName in featureNames)
            {
                if (mFeatures.ContainsKey(featureName))
                {
                    double avg = GetAvg(featureName);
                    double var = Math.Pow(GetStdDev(featureName), 2);
                    double otherAvg = otherAuthor.GetAvg(featureName);
                    double otherVar = Math.Pow(otherAuthor.GetStdDev(featureName), 2);
                    diff.Add(featureName, Math.Abs(avg - otherAvg));
                    stdDev.Add(featureName, Math.Sqrt(var + otherVar)); // http://stattrek.com/random-variable/combination.aspx
                }
                else
                {
                    SparseVector<double> vec = mFeatureVectors[featureName];
                    SparseVector<double> otherVec = otherAuthor.mFeatureVectors[featureName];
                    double cosSim = CosineSimilarity.Instance.GetSimilarity(vec, otherVec);
                    diff.Add(featureName, 1.0 - cosSim);                
                }
            }
        }
    }

    public class Text
    {
        public string mName;
        public string mAuthor;
        public ArrayList<Sentence> mSentences
            = new ArrayList<Sentence>();
        public string mHtmlFileName
            = Guid.NewGuid().ToString("N") + ".html";

        public Text(Corpus corpus, string name, string author)
        {
            mName = name;
            mAuthor = author;
            Sentence sentence = new Sentence();
            foreach (TaggedWord taggedWord in corpus.TaggedWords)
            {
                string tag = taggedWord.Tag;
                Token token = new Token();
                if (tag != null && tag.EndsWith("<eos>"))
                {
                    tag = tag.Substring(0, tag.Length - 5);
                }
                if (taggedWord.MoreInfo.Punctuation)
                {
                    token.mIsPunctuation = true;
                    token.mTokenStr = token.mLemma = token.mTag = taggedWord.Word;
                }
                else // word
                {
                    token.mTokenStr = taggedWord.Word;
                    token.mLemma = taggedWord.Lemma;
                    token.mTag = tag;
                }
                if (taggedWord.MoreInfo.FollowedBySpace)
                {
                    token.mIsFollowedBySpace = true;
                }
                sentence.mTokens.Add(token);
                if (taggedWord.MoreInfo.EndOfSentence)
                {
                    mSentences.Add(sentence); 
                    sentence = new Sentence();
                }
            }
            if (sentence.mTokens.Count > 0) 
            { 
                mSentences.Add(sentence); 
            }
        }

        public string GetHtml()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Sentence sentence in mSentences)
            {
                sb.Append("<span class='sentence'>");
                foreach (Token token in sentence.mTokens)
                {
                    if (!token.mIsPunctuation)
                    {
                        sb.Append(string.Format("<span class='token' data-toggle='tooltip' title='Oznaka: {0}&lt;br&gt;Lema: {1}'>",
                            HttpUtility.HtmlEncode(token.mTag).Replace("'", "&#39;"), HttpUtility.HtmlEncode(token.mLemma).Replace("'", "&#39;")));
                    }
                    else
                    {
                        sb.Append(string.Format("<span class='token'>"));
                    }
                    sb.Append(HttpUtility.HtmlEncode(token.mTokenStr));
                    sb.Append("</span>");
                    if (token.mIsFollowedBySpace) { sb.Append(" "); }
                }
                sb.Append("</span>");
            }
            return sb.ToString();
        }
    }
}
