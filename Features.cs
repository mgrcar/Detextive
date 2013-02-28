﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Latino;

namespace Detextive
{
    public static class Features
    {
        public static ArrayList<KeyDat<int, string>> GetFrequentWordsVector(Text text, Set<string> filter, bool lemmas)
        {
            MultiSet<string> tokens = new MultiSet<string>();
            foreach (Sentence sentence in text.mSentences)
            {
                foreach (Token token in sentence.mTokens)
                {
                    if (!token.mIsPunctuation)
                    {
                        string tokenLwr = token.mTokenStr.ToLower();
                        string lemmaLwr = token.mLemma.ToLower();
                        if (!lemmas && filter.Contains(tokenLwr))
                        {
                            tokens.Add(tokenLwr);
                        }
                        else if (lemmas && filter.Contains(lemmaLwr))
                        {
                            tokens.Add(lemmaLwr);
                        }
                    }
                }
            }
            ArrayList<KeyDat<int, string>> list = tokens.ToList();
            list.Sort(DescSort<KeyDat<int, string>>.Instance);
            return list;
        }

        public static ArrayList<KeyDat<int, string>> GetFunctionWordsVector(Text text)
        {
            MultiSet<string> functionWords = new MultiSet<string>();
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
                            functionWords.Add(token.mTokenStr.ToLower());
                        }
                    }
                }
            }
            ArrayList<KeyDat<int, string>> list = functionWords.ToList();
            list.Sort(DescSort<KeyDat<int, string>>.Instance);
            return list;
        }

        private static int CountSyllables(string word) // *** what about "r"?
        {
            int c = 0;
            ArrayList<char> vowels = new ArrayList<char>(new char[] { 'í', 'é', 'ê', 'á', 'ô', 'ó', 'ú', 'ì', 'è', 'à', 'ò', 'ù', 'i', 'e', 'a', 'o', 'u' }); 
            foreach (char ch in word)
            {
                if (vowels.Contains(char.ToLower(ch))) { c++; }
            }
            return c;
        }

        private static void GetReadabilityFeatures(Text txt, out double rWords, out double rChars, out double rSyllables, out double rComplex, out int numWords)
        {
            numWords = 0;
            int numSentences = 0;
            int numChars = 0;
            int numSyllables = 0;
            int numComplexWords = 0;
            foreach (Sentence stc in txt.mSentences)
            {
                //Console.WriteLine("Sentence: " + stc.Text);
                int numWordsThis = 0;
                int numCharsThis = 0;
                int numSyllablesThis = 0;
                int numComplexWordsThis = 0;
                foreach (Token tkn in stc.mTokens)
                {
                    if (!tkn.mIsPunctuation)
                    {
                        numWordsThis++;
                        numCharsThis += tkn.mTokenStr.Length;
                        int tmp = CountSyllables(tkn.mTokenStr);
                        numSyllablesThis += tmp;
                        if (tmp > 2) { numComplexWordsThis++; }
                    }
                }
                if (numWordsThis > 0) { numSentences++; }
                numWords += numWordsThis;
                numChars += numCharsThis;
                numSyllables += numSyllablesThis;
                numComplexWords += numComplexWordsThis;
            }
            rWords = numSentences == 0 ? 0.0 : ((double)numWords / (double)numSentences);
            rChars = numWords == 0 ? 0.0 : ((double)numChars / (double)numWords);
            rSyllables = numWords == 0 ? 0.0 : ((double)numSyllables / (double)numWords);
            rComplex = numWords == 0 ? 0.0 : ((double)numComplexWords / (double)numWords);
        }

        public static void GetReadabilityFeatures(Text txt, out double ari, out double flesch, out double fog, out double rWords, out double rChars, out double rSyllables, out double rComplex)
        {
            int numWords;
            GetReadabilityFeatures(txt, out rWords, out rChars, out rSyllables, out rComplex, out numWords);
            ari = 0.5 * rWords + 4.71 * rChars - 21.43;
            flesch = 206.835 - 1.015 * rWords - 84.6 * rSyllables;
            fog = 0.4 * (rWords + 100.0 * rComplex);
        }

        public static void GetVocabularyRichness(Text text, out double ttr, out double hl, out double honore, out double brunet)
        {
            // type-token ratio (TTR)
            MultiSet<string> tokens = new MultiSet<string>();          
            int n = 0;
            foreach (Sentence sentence in text.mSentences)
            {
                foreach (Token token in sentence.mTokens)
                {
                    if (!token.mIsPunctuation) 
                    {
                        tokens.Add(token.mTokenStr.ToLower()); // *** should I take lemma here?
                        n++;
                    }
                }
            }
            int v = tokens.CountUnique;
            ttr = (double)v / (double)n;
            // hapax legomena
            int v1 = tokens.ToList().Count(x => x.Key == 1);
            hl = (double)v1 / (double)n;
            // Honore's statistic: R = 100 x log(N) / (1 - V1 / V)
            honore = 100.0 * Math.Log(n) / (1.0 - (double)v1 / (double)v);
            // Brunet's index: W = N^(V^-0.165)
            brunet = Math.Pow(n, Math.Pow(v, -0.165));
        }
    }
}
