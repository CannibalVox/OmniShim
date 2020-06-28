using System;
namespace omnishim
{
    public class OmniShimSystem
    {
        public OmniShimSystem()
        {
        }

        public void PreStart()
        {
            new OmniShim();
        }
    }
}
