using System;
using System.Collections;
using Latino.TextMining;
using Latino;

namespace Detextive
{
    public class ArrayTokenizerEnumerator : ITokenizerEnumerator
    {
        private ArrayList<string> mTokens;
        private int mIdx 
            = -1;

        public ArrayTokenizerEnumerator(ArrayList<string> tokens)
        {
            mTokens = tokens;
        }

        public string Current 
        {
            get { return mTokens[mIdx]; } 
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public Pair<int, int> CurrentPos
        {
            get { throw new NotImplementedException(); } 
        }

        public bool MoveNext()
        {
            mIdx++;
            if (mIdx == mTokens.Count) 
            { 
                Reset(); 
                return false; 
            }
            return true;
        }

        public void Reset()
        {
            mIdx = -1;
        }

        public void Dispose()
        {
        }
    }
}
