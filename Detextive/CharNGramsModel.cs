using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;
using Latino.Model;

namespace Detextive
{
    public class CharNGramsModel : ModelBase
    {
        public CharNGramsModel()
        {
            mSelector = "cng";
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
            ArrayList<SparseVector<double>> bows = mBowSpace.InitializeTokenized(texts.Select(x => (ITokenizer)new Tokenizer(x)), /*largeScale=*/false);
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
            private int MAX_NGRAM_LEN
                = Convert.ToInt32(Utils.GetConfigValue("CharNGramsMaxLen", "3"));

            public Tokenizer(Text text)
            {
                Tokenize(text);
            }

            private void Tokenize(Text text)
            {
                for (int n = 1; n <= MAX_NGRAM_LEN; n++)
                {
                    foreach (Sentence sentence in text.mSentences)
                    {
                        Queue<char> queue = new Queue<char>();
                        for (int i = 0; i < n; i++) { queue.Enqueue('_'); } // padding left
                        foreach (Token token in sentence.mTokens)
                        {
                            string tokenStr = token.mTokenStr;
                            foreach (char ch in tokenStr)
                            {
                                queue.Enqueue(ch); queue.Dequeue();
                                mTokens.Add(new string(queue.ToArray()));
                            }
                            if (token.mIsFollowedBySpace)
                            {
                                queue.Enqueue('_'); queue.Dequeue();
                                mTokens.Add(new string(queue.ToArray()));
                            }
                        }
                        // padding right
                        string empty = new string('_', n);
                        for (int i = 0; i < n - 1; i++) 
                        {
                            queue.Enqueue('_'); queue.Dequeue();
                            string token = new string(queue.ToArray());
                            if (token == empty) { break; }
                            mTokens.Add(token);
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
