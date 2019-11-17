using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Acme.Clone
{
  public class Clone
  {
    public static Object DeepClone(Object original)
    {
      using (MemoryStream ms = new MemoryStream())
      {
        BinaryFormatter formatter = new BinaryFormatter();

        formatter.Context = new StreamingContext(StreamingContextStates.Clone);

        formatter.Serialize(ms, original);

        ms.Position = 0;

        return formatter.Deserialize(ms);
      }
    }
  }

  [Serializable]
  public class CloneTestClass
  {
    //Object 也有这个attribute。
    public string Name { get; set; }
    public int Age { get; set; }
  }

  public class SeriClassA
  {
    public string Name { get; set; }
  }

  [Serializable]
  public class SeriClassB : SeriClassA
  {
    //Serializable 类似一个继承链，链中一个断了，下面的就没了。
  }
}