using PersistorEngine;
using System.Collections.Generic;

public class Purse : PersistorMonoBehaviour
{
   [Persist]public List<string> items = new();
}
