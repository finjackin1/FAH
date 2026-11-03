// Loads module .dll files from modulos\ (one AssemblyLoadContext each, read into memory so files are not locked).
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using FAH.Contrato;

namespace FAH;

static class Carregador
{
    sealed class Contexto(string nome, string pasta) : AssemblyLoadContext(nome, isCollectible: false)
    {
        // the contract always comes from the host (same type identity); a module's own libraries (e.g. onnxruntime) sit next to it in modulos\
        protected override Assembly? Load(AssemblyName n)
        {
            if (n.Name == "FAH.Contrato") return typeof(IModulo).Assembly;
            var arq = Path.Combine(pasta, n.Name + ".dll");
            return n.Name != null && !n.Name.StartsWith("FAH.") && File.Exists(arq) ? LoadFromAssemblyPath(arq) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string nome)
        {
            var arq = Path.Combine(pasta, nome.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? nome : nome + ".dll");
            return File.Exists(arq) ? LoadUnmanagedDllFromPath(arq) : IntPtr.Zero;
        }
    }

    public static List<IModulo> Carregar(string pasta, List<string> erros)
    {
        var lst = new List<IModulo>();
        foreach (var arq in Directory.GetFiles(pasta, "FAH.*.dll").Order())
        {
            try
            {
                var ctx = new Contexto(Path.GetFileNameWithoutExtension(arq), pasta);
                using var ms = new MemoryStream(File.ReadAllBytes(arq));
                var asm = ctx.LoadFromStream(ms);
                foreach (var t in asm.GetTypes())
                {
                    var at = t.GetCustomAttribute<ModuloFAH>();
                    if (at == null || !typeof(IModulo).IsAssignableFrom(t)) continue;
                    if (at.Contrato != Versao.Contrato) { erros.Add($"{Path.GetFileName(arq)}: contrato {at.Contrato} (FAH usa {Versao.Contrato})"); continue; }
                    lst.Add((IModulo)Activator.CreateInstance(t)!);
                }
            }
            catch (Exception e) { var x = e.InnerException ?? e; erros.Add($"{Path.GetFileName(arq)}: {x.GetType().Name}: {x.Message}"); }
        }
        return lst;
    }
}
