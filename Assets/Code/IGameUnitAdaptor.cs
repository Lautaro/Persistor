using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public interface IGameUnitAdaptor<T>
{
    void CopyToData(T adaptorTarget);
    void CopyFromData(T adaptorTarget);
}
