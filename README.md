# OmniShim

Dependency mediation for VintageStory mods

## What is Dependency Mediation?

In many modding systems, it is not uncommon for mods to work together to provide a richer experience.  It is not uncommon for large mods
 that make serious changes to the base game to provide an API for the benefit of other modders.

However, what is the cost of including other mods' APIs?  How can allow users to deal gracefully with version mismatches?  How can we allow
a mod to automatically switch off compatibility features when the compatible mod is not present?  

Dependency Mediation is a process by which we allow a mod to interact with another mod's API without including any direct couplings between
 the two mods.  By including and interacting with OmniShim, one mod can expose an API to another without direct code interaction.

## How does it work?

Let's say that I have built a mod that gives many blocks a "wealth" value, and I include Block Behaviors that allow wealth values to be attached
  to arbitrary blocks. Consider the following class:

```cs
public class BlockBehaviorWealthHaver : BlockBehavior {
    public double Wealth {get; private set;}

    public BlockBehaviorWealthHaver(Block block) : base(block) {}

    public override void Initialize(JsonObject property) {
        //Get wealth from json
    }
}
```

Then, when I need to get a block's wealth, if any, I can do the following:

```cs
    public double WealthForBlock(Block block) {
        BlockBehaviorWealthHaver behavior = block.GetBehavior(typeof(BlockBehaviorWealthHaver));
        if (behavior == null) return 0;

        return behavior.Wealth;
    }
```

However, let's say that you would like to write a mod that adds a few blocks whose wealth is determined by a complex algorithm.  And as a mod author
 whose mod gets more valuable the more blocks have attached wealth values, I'd like to help you!  So, I create an interface:

```cs
    public interface IWealthHaver {
        double Wealth {get;}
    }

    public class BlockBehaviorWealthHaver : BlockBehavior, IWealthHaver {
        ...
    }
```

And I access block wealth via that interface:

```cs
    public double WealthForBlock(Block block) {
        BlockBehaviorWealthHaver behavior = block.GetBehavior(typeof(IWealthHaver), true);
        if (behavior == null) return 0;

        return behavior.Wealth;
    }
```

Now all you need to do is create a new BlockBehavior that overrides the interface!  But also, I have to vend a thin API, and you have to depend
  on it.  And you need to make sure your mod acts appropriately when my mod isn't present.  Instead, imagine using OmniShim:

```cs
public class BlockBehaviorWealthCompat : BlockBehavior {
    public virtual double Wealth { get; set; }
    ...
}

public class WealthCompatSystem : ModSystem {
    public override Start(ICoreAPI api) {
        Type wrappedType = OmniShim.RegisterForInterface<BlockBehaviorWealthCompat>("me.mymod.IWealthHaver");
        api.RegisterBlockBehaviorClass("newwealthhaver", wrappedType);
    }
}
```

Now, my BlockBehavior class effectively implements your interface, and our mods never had to talk.  All I had to do was provide the virtual
 members to meet your mods interface, and register my class.  OmniShim created a new class inherited from mine that implements your interface.

### Proto Mirroring

Overriding interface methods with no parameters is easy enough, but what if an interface's method signatures include types that are also
 owned by the host mod?  What can we do to allow the client mod to implement the interface?  One thing we can do is deliver data parameters
 in either primitive types, or in protobufs.  If an interface method accepts or returns a ProtoContract type, it's easy enough to allow
 the client mod create a compatible type.  Consider the following change:

```cs
[ProtoContract]
public class WealthData {
    [ProtoMember(1)]
    public double Value;
}

public interface IWealthHaver {
    WealthData Wealth {get;}
}
```

Now let's implement the client mod:
```cs
[ProtoContract]
public class LocalWealthData {
    [ProtoMember(1)]
    public double Value;
}


public class BlockBehaviorWealthCompat : BlockBehavior {
    public virtual LocalWealthData Wealth { get; set; }
    ...
}

public class WealthCompatSystem : ModSystem {
    public override Start(ICoreAPI api) {
        OmniShim.RegisterMirrorProto<LocalWealthData>("me.mymod.WealthData");
        Type wrappedType = OmniShim.RegisterForInterface<BlockBehaviorWealthCompat>("me.mymod.IWealthHaver");
        api.RegisterBlockBehaviorClass("newwealthhaver", wrappedType);
    }
}
```

Through the magic of proto, you don't have to name the fields the same thing, or even include the entire host version of the ProtoContract.
 You can simply include the fields you care about, and make sure they have the same field keys (the numbers passed to `ProtoMember()`).

## Performance?

We don't have hard performance numbers yet, but you don't have to worry about the performance overhead of using reflection: at registration
 time, OmniShim uses reflection to generate types, but it uses emitted IL to execute the shim at runtime.  This should produce reasonably-fast
 shimming with very little reflection overhead.  You should be aware that Proto Mirroring uses Serializer.ChangeType<>() at runtime, so
 all calls will likely incur an allocation.  Additionally, the VSAPI method block.GetBehavior() uses reflection- we will likely include a memoization
 helper in a future version.

## Status

OmniShim is not quite production ready yet- we will be refining error handling, building out the VS integrations, and adding Interface Mirroring
 to go with Protobuf Mirroring very soon.
