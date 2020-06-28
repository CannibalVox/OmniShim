using System;
using NUnit.Framework;
using omnishim;

namespace omnishim_test_core
{
    public class FailedRegisterClass
    {
        private string value;

        public FailedRegisterClass() { }
        public FailedRegisterClass(string value)
        {
            this.value = value;
        }
    }

    public class SuccessfulRegisterClass
    {
        private string value;

        public SuccessfulRegisterClass() { }
        public SuccessfulRegisterClass(string value)
        {
            this.value = value;
        }

        public virtual string DoThing(string name)
        {
            if (!string.IsNullOrEmpty(value)) return value;
            return name;
        }
    }

    public interface ISimpleInterface
    {
        string DoThing(string name);
    }

    [TestFixture()]
    public class RegisterForInterfaceTests
    {
        [SetUp()]
        public void SetUp()
        {
            var system = new OmniShimSystem();
            system.PreStart();
        }

        [Test()]
        public void RegisterForInterface_NoInterfaceImplementation_ThrowsException()
        {
            Assert.Throws(typeof(TypeLoadException), () => OmniShim.RegisterForInterface<FailedRegisterClass>("omnishim_test_core.ISimpleInterface"));
        }

        [Test()]
        public void RegisterForInterface_Implementation_SuccessfullyCallInterface()
        {
            var wrapType = OmniShim.RegisterForInterface<SuccessfulRegisterClass>("omnishim_test_core.ISimpleInterface");
            ISimpleInterface transform = (ISimpleInterface)Activator.CreateInstance(wrapType);
            Assert.AreEqual("test1", transform.DoThing("test1"));
        }

        [Test()]
        public void RegisterForInterface_Implementation_SuccessfullyCreateWithNonDefaultConstructor()
        {
            var wrapType = OmniShim.RegisterForInterface<SuccessfulRegisterClass>("omnishim_test_core.ISimpleInterface");
            ISimpleInterface transform = (ISimpleInterface)Activator.CreateInstance(wrapType, "test2");
            Assert.AreEqual("test2", transform.DoThing("test1"));
        }
    }
}
