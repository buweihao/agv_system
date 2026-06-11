using System;
using Prism.DryIoc;
using Prism.Ioc;
using System.Windows;
class App : PrismApplication {
    protected override Window CreateShell() { Console.WriteLine(""CreateShell""); return new Window(); }
    protected override void RegisterTypes(IContainerRegistry r) { Console.WriteLine(""RegisterTypes""); }
}
Console.WriteLine(""Ready"");
