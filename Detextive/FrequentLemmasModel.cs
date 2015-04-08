using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;
using Latino.Model;

namespace Detextive
{
    public class FrequentLemmasModel : ModelBase
    {
        public FrequentLemmasModel()
        {
            mSelector = "frl";
            mBowSpace.CutLowWeightsPerc = 0;
            mBowSpace.MaxNGramLen = 1;
            mBowSpace.MinWordFreq = 1; 
            mBowSpace.NormalizeVectors = true; 
            mBowSpace.Stemmer = null;
            mBowSpace.WordWeightType = GetWeightTypeConfig();            
        }

        public void Initialize(IEnumerable<Author> authors)
        {
            IEnumerable<Text> texts = authors.SelectMany(x => x.mTexts);
            // compute most frequent lemmas
            MultiSet<string> tokens = new MultiSet<string>();
            foreach (Text text in texts)
            {
                foreach (Sentence sentence in text.mSentences)
                {
                    foreach (Token token in sentence.mTokens)
                    {
                        if (!token.mIsPunctuation)
                        {
                            tokens.Add(token.mLemma.ToLower());
                        }
                    }
                }
            }
            Set<string> filter = new Set<string>(
                tokens.ToList()
                .OrderByDescending(x => x.Key)
                .Take(Convert.ToInt32(Utils.GetConfigValue("NumFrequentLemmas", "100")))
                .Select(x => x.Dat));
            ArrayList<SparseVector<double>> bows = mBowSpace.InitializeTokenized(texts.Select(x => new Tokenizer(x, filter).GetTokens(null)), /*largeScale=*/false);
            int i = 0;
            foreach (Text text in texts) 
            { 
                text.mFeatureVectors.Add(mSelector, TransformVector(bows[i++]));
            }
            TrainModels(authors);
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
                            string tokenStr = token.mLemma.ToLower();
                            if (filter.Contains(tokenStr)) { mTokens.Add(tokenStr); }
                        }
                    }
                }
            }

            public ITokenizerEnumerable GetTokens(string text)
            {
                return new TokenizerEnumerable(new ArrayTokenizerEnumerator(mTokens));
            }

            public void Save(BinarySerializer writer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
