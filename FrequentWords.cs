using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;

namespace Detextive
{
    public static class FrequentWords
    {
        public static BowSpace mBowSpace
            = new BowSpace();

        static FrequentWords()
        {
            mBowSpace.CutLowWeightsPerc = 0;
            mBowSpace.MaxNGramLen = 1;
            mBowSpace.MinWordFreq = 1;
            mBowSpace.NormalizeVectors = true;
            mBowSpace.Stemmer = null;
            mBowSpace.WordWeightType = WordWeightType.TermFreq; // *** config
        }

        public static void Initialize(IEnumerable<Text> texts, out Set<string> _filter)
        {
            // compute most frequent words
            MultiSet<string> tokens = new MultiSet<string>();
            foreach (Text text in texts)
            {
                foreach (Sentence sentence in text.mSentences)
                {
                    foreach (Token token in sentence.mTokens)
                    {
                        if (!token.mIsPunctuation)
                        {
                            tokens.Add(token.mTokenStr.ToLower());
                        }
                    }
                }
            }
            Set<string> filter = _filter = new Set<string>(
                tokens.ToList()
                .OrderByDescending(x => x.Key)
                .Take(100) // *** config
                .Select(x => x.Dat));
            ArrayList<SparseVector<double>> bows = mBowSpace.InitializeTokenized(texts.Select(x => (ITokenizer)new Tokenizer(x, filter)), /*largeScale=*/false);
            int i = 0;
            foreach (Text text in texts) { text.mFeatureVectors.Add("frw", bows[i++]); }
        }

        public class Tokenizer : ITokenizer
        {
            private ArrayList<string> mTokens
                = new ArrayList<string>();

            public Tokenizer(Text text, Set<string> filter)
            {
                Tokenize(text, filter);                
            }

            private void Tokenize(Text text, Set<string> filter)
            {
                foreach (Sentence sentence in text.mSentences)
                {
                    foreach (Token token in sentence.mTokens)
                    {
                        if (!token.mIsPunctuation)
                        {
                            string tokenStr = token.mTokenStr.ToLower();
                            if (filter.Contains(tokenStr)) { mTokens.Add(tokenStr); }
                        }
                    }
                }
            }

            public string Text
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public ITokenizerEnumerator GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<string> IEnumerable<string>.GetEnumerator()
            {
                return mTokens.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Save(BinarySerializer writer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
