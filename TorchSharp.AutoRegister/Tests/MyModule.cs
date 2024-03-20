using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TorchSharp;
using TorchSharp.AutoRegister;
using TorchSharp.Modules;
using static TorchSharp.torch;

namespace Tests;

internal partial class MyModule : nn.Module<Tensor, Tensor>
{
    [AutoRegistered]
    private Linear linear;

    public MyModule() : base(nameof(MyModule))
    {
        this.Linear = nn.Linear(1, 1);
    }

    public override Tensor forward(Tensor input)
    {
        return linear.forward(input);
    }
}
