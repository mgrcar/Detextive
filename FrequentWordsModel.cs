﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;
using Latino.Model;

namespace Detextive
{
    public class FrequentWordsModel : ModelBase
    {
        public FrequentWordsModel()
        {
            mBowSpace.CutLowWeightsPerc = 0;
            mBowSpace.MaxNGramLen = 1;
            mBowSpace.MinWordFreq = 1;
            mBowSpace.NormalizeVectors = true;
            mBowSpace.Stemmer = null;
            mBowSpace.WordWeightType = GetWeightTypeConfig("FrequentWordsWeightType");
        }

        public void Initialize(IEnumerable<Text> texts)
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
            Set<string> filter = new Set<string>(
                tokens.ToList()
                .OrderByDescending(x => x.Key)
                .Take(Convert.ToInt32(Utils.GetConfigValue("NumFrequentWords", "100")))
                .Select(x => x.Dat));
            ArrayList<SparseVector<double>> bows = mBowSpace.InitializeTokenized(texts.Select(x => (ITokenizer)new Tokenizer(x, filter)), /*largeScale=*/false);
            int i = 0;
            foreach (Text text in texts) 
            { 
                text.mFeatureVectors.Add("frw", bows[i]);
                //if (!text.mIsTestText) 
                { 
                    mDataset.Add(new LabeledExample<string, SparseVector<double>>(text.mAuthor, bows[i])); 
                }
                i++;
            }
            TrainModels();
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
