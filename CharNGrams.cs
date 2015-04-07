using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Latino;
using Latino.TextMining;

namespace Detextive
{
    public static class CharNGrams
    {
        public static BowSpace mBowSpace
            = new BowSpace();

        static CharNGrams()
        {
            mBowSpace.CutLowWeightsPerc = 0;
            mBowSpace.MaxNGramLen = 1;
            mBowSpace.MinWordFreq = 1; // *** config
            mBowSpace.NormalizeVectors = true; // *** config
            mBowSpace.Stemmer = null;
            mBowSpace.WordWeightType = WordWeightType.TfIdf; // *** config
        }

        public static ArrayList<SparseVector<double>> Initialize(IEnumerable<Text> texts)
        {
            return mBowSpace.InitializeTokenized(texts.Select(x => (ITokenizer)new Tokenizer(x)), /*largeScale=*/false);
        }

        public class Tokenizer : ITokenizer
        {
            private ArrayList<string> mTokens
                = new ArrayList<string>();
            private int MAX_NGRAM_LEN
                = 3;

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
                        // TODO: padding right
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
