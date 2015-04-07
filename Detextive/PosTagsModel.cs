using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;
using Latino.Model;

namespace Detextive
{
    public class PosTagsModel : ModelBase
    {
        public PosTagsModel()
        {
            mSelector = "pos";
            mBowSpace.CutLowWeightsPerc = 0;
            mBowSpace.MaxNGramLen = Convert.ToInt32(Utils.GetConfigValue("PosTagsMaxSeqLen", "2"));
            mBowSpace.MinWordFreq = 1; 
            mBowSpace.NormalizeVectors = true; 
            mBowSpace.Stemmer = null;        
            mBowSpace.WordWeightType = GetWeightTypeConfig();            
        }

        public void Initialize(IEnumerable<Author> authors)
        {
            IEnumerable<Text> texts = authors.SelectMany(x => x.mTexts);
            ArrayList<SparseVector<double>> bows = mBowSpace.InitializeTokenized(texts.Select(x => new Tokenizer(x).GetTokens(null)), /*largeScale=*/false);
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

            public Tokenizer(Text text)
            {
                Tokenize(text);
            }

            private void Tokenize(Text text)
            {
                foreach (Sentence s in text.mSentences)
                {
                    foreach (Token t in s.mTokens)
                    {
                        mTokens.Add(t.mTagReduced); // *** this includes punctuation
                    }
                }
            }

            public string Text
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
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
