using System.ServiceModel;

namespace ABPMilkDryfeHiberSrv
{
    [ServiceContract(Namespace = "http://ABPMilkDryfeHiberSrv")]
    public interface IService
    {
        [OperationContract]
        void SendShutdown();
    }
}
