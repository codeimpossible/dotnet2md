using System.Reflection;

namespace DotnetToMd
{
    /// <summary>
    /// This will process C# xml documentation and metadata and generate a markdown format,
    /// optimized for mdBook.
    /// </summary>
    internal class Entrypoint
    {
        /// <summary>
        /// This will generate the markdown files based on a .xml path.
        /// </summary>
        /// <param name="args">
        /// This expects two arguments:
        ///  1. Path to the output folder, relative to the executable or absolute.
        ///  2. Target assembly name.
        ///  3. Output path of the markdown files.
        /// </param>
        /// <exception cref="ArgumentException">Whenever the arguments mismatch the expectation of <paramref name="args"/>.</exception>
        internal static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Arguments were invalid.\nExpected: Parser.exe <xml_path> <out_path> <targets>\n" +
                    "  * <xml_path>\tpath to the .xml;\n" +
                    "  * <out_path>\toutput path\n" +
                    "  * <targets>\ttarget assemblies to scan.\n");
                Console.ResetColor();

                return 1;
            }

            var sourcePath = ProcessPathToRoot(args[0]);
            var outputPath = ProcessPathToRoot(args[1]);

            // Name of the target assembly which we will scan.
            List<string> targetAssemblies = new();
            for (var i = 2; i < args.Length; i++)
            {
                targetAssemblies.AddRange(args[i].Split(' '));
            }

            try
            {
                Parse(sourcePath, outputPath, targetAssemblies);
            }
            catch (ReflectionTypeLoadException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();

                Console.WriteLine($"Make sure your project has all the dependencies reachable from '{sourcePath}'.");
                return 1;
            }
            catch (Exception e)
            {
                // Write output before exiting.
                Console.ForegroundColor = ConsoleColor.Red;
                var iex = e;
                while (iex != null)
                {
                    Console.WriteLine(iex.Message);
                    Console.WriteLine($"-----------------------------------------------------------------------------");
                    Console.WriteLine(iex.StackTrace);
                    iex = iex.InnerException;
                }
                Console.ResetColor();

                return 1;
            }

            Console.WriteLine("Finished generating markdown files!");
            return 0;
        }

        /// <summary>
        /// Public entrypoint if called as a library.
        /// </summary>
        public static void Parse(string sourcePath, string outputPath, IEnumerable<string> targetAssemblies)
        {
            CreateIfNotFound(outputPath);

            Console.WriteLine($"sourcePath: {sourcePath}");
            Console.WriteLine($"outputPath: {outputPath}");

            string[] xmlFiles = GetXmlFilePaths(sourcePath).ToArray();
            if (xmlFiles.Count() == 0)
            {
                throw new ArgumentException("No .xml path was found. Please revisit the output path.");
            }

            IEnumerable<string> allAssemblies = GetAllLibrariesInPath(sourcePath);
            if (!allAssemblies.Any())
            {
                throw new InvalidOperationException("Unable to find the any binaries. Have you built the target project?");
            }

            List<Assembly> assembliesToScan = new();

            List<Assembly> dependencies = new();
            foreach (var assembly in allAssemblies)
            {
                try
                {
                    var asm = Assembly.LoadFrom(assembly);
                    dependencies.Add(asm);

                    foreach (var targetAssembly in targetAssemblies)
                    {
                        if (asm.ManifestModule.Name.Equals($"{targetAssembly}.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            assembliesToScan.Add(asm);
                        }
                    }
                }
                catch (Exception e) when (e is FileLoadException || e is BadImageFormatException)
                {
                    // Ignore invalid (or native) assemblies.
                }
            }

            if (assembliesToScan.Count == 0)
            {
                throw new InvalidOperationException($"Unable to find any of the target assemblies. Did you pass the correct name?");
            }

            Parser parser = new(assembliesToScan, dependencies, xmlFiles, outputPath);
            parser.Generate();
        }

        /// <summary>
        /// Look recursively for all the files in <paramref name="path"/>.
        /// </summary>
        /// <param name="path">Rooted path to the binaries folder. This must be a valid directory.</param>
        private static IEnumerable<string> GetAllLibrariesInPath(in string path) =>
            Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories);

        /// <summary>
        /// Look recursively for all the files in <paramref name="path"/> with a .xml file.
        /// </summary>
        /// <param name="path">Rooted path to the binaries folder. This must be a valid directory.</param>
        private static IEnumerable<string> GetXmlFilePaths(in string path)
        {
            return Directory.EnumerateFiles(path, "*.xml", SearchOption.AllDirectories);
        }

        /// <summary>
        /// Create a directory at <paramref name="path"/> if none is found.
        /// </summary>
        private static void CreateIfNotFound(in string path)
        {
            if (!Directory.Exists(path))
            {
                _ = Directory.CreateDirectory(path);
            }
        }

        private static string ProcessPathToRoot(in string path)
        {
            if (!Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path, Environment.CurrentDirectory);
                // return Path.GetFullPath(Path.Join(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), path));
            }

            return path;
        }
    }
}
