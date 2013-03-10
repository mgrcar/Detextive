using System;
using System.Linq;
using System.Collections.Generic;
using Latino;
using Latino.Model;
using Latino.TextMining;

namespace Detextive
{
    public class ModelBase
    {
        public BowSpace mBowSpace
            = new BowSpace();
        //public IModel<string> mModel
        //    = null;
        public Dictionary<string, IModel<string>> mModels
            = new Dictionary<string, IModel<string>>();
        public string mSelector;
        
        public void TrainModels(IEnumerable<Author> authors)
        {
            //LabeledDataset<string, SparseVector<double>> ds = new LabeledDataset<string, SparseVector<double>>();
            //foreach (Author author in authors)
            //{
            //    foreach (Text text in author.mTexts)
            //    {
            //        ds.Add(new LabeledExample<string, SparseVector<double>>(author.mName, text.mFeatureVectors[mSelector]));
            //    }
            //}
            //mModel = new SvmMulticlassFast<string>();
            //SvmMulticlassFast<string> model = (SvmMulticlassFast<string>)mModel;
            //model.C = Convert.ToDouble(Utils.GetConfigValue("SvmMultiClassC", "5000"));
            //model.Train(ds);
            foreach (Author author in authors)
            {
                LabeledDataset<string, SparseVector<double>> ds = new LabeledDataset<string, SparseVector<double>>();
                foreach (Author otherAuthor in authors)
                {
                    if (otherAuthor != author)
                    {
                        foreach (Text text in otherAuthor.mTexts)
                        {
                            ds.Add(new LabeledExample<string, SparseVector<double>>(otherAuthor.mName, text.mFeatureVectors[mSelector]));
                        }
                    }
                }
                SvmMulticlassFast<string> model = new SvmMulticlassFast<string>();
                model.C = Convert.ToDouble(Utils.GetConfigValue("SvmMultiClassC", "5000"));
                model.Train(ds);
                mModels.Add(author.mName, model);
            }
        }

        public WordWeightType GetWeightTypeConfig()
        {
            return Utils.GetConfigValue("WeightType_" + mSelector, "").ToLower() == "tfidf" ? WordWeightType.TfIdf : WordWeightType.TermFreq;
        }

        public SparseVector<double> TransformVector(SparseVector<double> vec) // apply log-relative weighting
        {
            if (Utils.GetConfigValue("WeightType_" + mSelector, "").ToLower() == "logrel")
            {
                double sum = vec.Sum(x => x.Dat);
                if (sum > 0)
                {
                    SparseVector<double> newVec = new SparseVector<double>(vec.Count);
                    foreach (IdxDat<double> item in vec) 
                    { 
                        newVec.InnerIdx.Add(item.Idx); 
                        newVec.InnerDat.Add(Math.Log(1.0 + item.Dat / sum)); 
                    }
                    ModelUtils.NrmVecL2(newVec);
                    vec = newVec;
                }
            }
            return vec;
        }
    }
}
