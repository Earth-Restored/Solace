namespace Solace.Common;

public interface ICloneable<TSelf>
    where TSelf : ICloneable<TSelf>
{
    TSelf DeepCopy();
}