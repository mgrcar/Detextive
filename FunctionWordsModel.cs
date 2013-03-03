using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;
using Latino.Model;

namespace Detextive
{
    public class FunctionWordsModel : ModelBase
    {
        public FunctionWordsModel()
        {
            mBowSpace.CutLowWeightsPerc = 0;
            mBowSpace.MaxNGramLen = 1;
            mBowSpace.MinWordFreq = 1; 
            mBowSpace.NormalizeVectors = true; 
            mBowSpace.Stemmer = null;
            mBowSpace.WordWeightType = GetWeightTypeConfig("FunctionWordsWeightType");
        }

        public void Initialize(IEnumerable<Text> texts)
        {
            ArrayList<SparseVector<double>> bows = mBowSpace.InitializeTokenized(texts.Select(x => (ITokenizer)new Tokenizer(x)), /*largeScale=*/false);
            int i = 0;
            foreach (Text text in texts) 
            { 
                text.mFeatureVectors.Add("fuw", bows[i]);
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

            public Tokenizer(Text text)
            {
                Tokenize(text);
            }

            private void Tokenize(Text text)
            {
                foreach (Sentence sentence in text.mSentences)
                {
                    foreach (Token token in sentence.mTokens)
                    {
                        if (!token.mIsPunctuation)
                        {
                            if (token.mTag.StartsWith("D") ||
                                token.mTag.StartsWith("Z") ||
                                token.mTag.StartsWith("V") ||
                                token.mTag.StartsWith("Gp") ||
                                token.mTag.StartsWith("M") ||
                                token.mTag.StartsWith("L"))
                            {
                                mTokens.Add(token.mTokenStr.ToLower());
                            }
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
