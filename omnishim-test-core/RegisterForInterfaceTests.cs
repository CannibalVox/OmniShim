using System;
using NUnit.Framework;
using omnishim;
using ProtoBuf;

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

    [ProtoContract]
    public struct SomeData
    {
        [ProtoMember(1)]
        public string Name;
        [ProtoMember(2)]
        public double Number;
        [ProtoMember(3)]
        public int Fixed;
    }

    public interface IProtoInterface
    {
        string DoThing(SomeData name);
        SomeData GetThing();
    }

    [ProtoContract]
    public struct OtherData
    {
        [ProtoMember(1)]
        public string LocalName;
    }

    public class ProtoRegisterClass
    {
        public virtual string DoThing(SomeData name)
        {
            return name.Name;
        }

        public virtual SomeData GetThing()
        {
            return new SomeData()
            {
                Name = "SomeDataOutput",
                Number = 1.1,
                Fixed = 2,
            };
        }
    }

    public class ProtoMirrorClass
    {
        public virtual string DoThing(OtherData name)
        {
            return name.LocalName;
        }

        public virtual OtherData GetThing()
        {
            return new OtherData()
            {
                LocalName = "OtherDataOutput",
            };
        }
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

        [Test()]
        public void RegisterForInterface_ImplementationWithProto_SuccessfullyCallInterface()
        {
            var wrapType = OmniShim.RegisterForInterface<ProtoRegisterClass>("omnishim_test_core.IProtoInterface");
            IProtoInterface transform = (IProtoInterface)Activator.CreateInstance(wrapType);
            Assert.AreEqual("test1", transform.DoThing(new SomeData()
            {
                Name = "test1",
                Number = 5.3,
                Fixed = 2,
            }));
        }

        [Test()]
        public void RegisterForInterface_ReturnImplementationWithProto_SuccessfullyCallInterface()
        {
            var wrapType = OmniShim.RegisterForInterface<ProtoRegisterClass>("omnishim_test_core.IProtoInterface");
            IProtoInterface transform = (IProtoInterface)Activator.CreateInstance(wrapType);
            Assert.AreEqual(new SomeData()
            {
                Name = "SomeDataOutput",
                Number = 1.1,
                Fixed = 2,
            }, transform.GetThing());
        }

        [Test()]
        public void RegisterForInterface_ImplementationWithMirrorProto_SuccessfullyCallInterface()
        {
            OmniShim.RegisterMirrorProto<OtherData>("omnishim_test_core.SomeData");
            var wrapType = OmniShim.RegisterForInterface<ProtoMirrorClass>("omnishim_test_core.IProtoInterface");
            IProtoInterface transform = (IProtoInterface)Activator.CreateInstance(wrapType);
            Assert.AreEqual("test1", transform.DoThing(new SomeData()
            {
                Name = "test1",
                Number = 5.3,
                Fixed = 2,
            }));
        }

        [Test()]
        public void RegisterForInterface_ReturnImplementationWithMirrorProto_SuccessfullyCallInterface()
        {
            OmniShim.RegisterMirrorProto<OtherData>("omnishim_test_core.SomeData");
            var wrapType = OmniShim.RegisterForInterface<ProtoMirrorClass>("omnishim_test_core.IProtoInterface");
            IProtoInterface transform = (IProtoInterface)Activator.CreateInstance(wrapType);
            Assert.AreEqual(new SomeData()
            {
                Name = "OtherDataOutput",
            }, transform.GetThing());
        }
    }
}
