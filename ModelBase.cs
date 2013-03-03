using Latino;
using Latino.Model;
using Latino.TextMining;

namespace Detextive
{
    public class ModelBase
    {
        public BowSpace mBowSpace
            = new BowSpace();
        public LabeledDataset<string, SparseVector<double>> mDataset
            = new LabeledDataset<string, SparseVector<double>>();
        public IModel<string> mModel
            = null;

        public void TrainModels()
        {
            mModel = new BatchUpdateCentroidClassifier<string>();
            BatchUpdateCentroidClassifier<string> model = (BatchUpdateCentroidClassifier<string>)mModel;
            model.Iterations = 0;
            model.Train(mDataset);
            //mModel = new KnnClassifierFast<string>();
            //KnnClassifierFast<string> model = (KnnClassifierFast<string>)mModel;
            //model.K = 1;
            //model.SoftVoting = false;
            //model.Train(mDataset);
        }

        public static WordWeightType GetWeightTypeConfig(string keyName)
        {
            return Utils.GetConfigValue(keyName, "").ToLower() == "tfidf" ? WordWeightType.TfIdf : WordWeightType.TermFreq;
        }
    }
}
