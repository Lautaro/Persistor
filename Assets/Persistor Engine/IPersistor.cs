using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public interface IPersistor
{
    void CopyToData(MonoBehaviour adaptorTarget);
    void CopyFromData(MonoBehaviour adaptorTarget);
}
