using System;

namespace Liquid.Domain.Test
{
    public abstract class LightTestDisposable : IDisposable
    {
        public abstract void Dispose();
    }
}