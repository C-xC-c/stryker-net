using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reflection;
using System.Text;
using Buildalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using Shouldly;
using Stryker.Core.Compiling;
using Stryker.Core.Exceptions;
using Stryker.Core.Initialisation;
using Stryker.Core.InjectedHelpers;
using Stryker.Core.Mutants;
using Stryker.Core.MutationTest;
using Stryker.Core.Mutators;
using Stryker.Core.Options;
using Stryker.Core.ProjectComponents;
using Stryker.Core.TestRunners;
using Xunit;

namespace Stryker.Core.UnitTest.Compiling
{
    public class CompilingProcessTests : TestBase
    {
        [Fact]
        public void CompilingProcessTests_ShouldCompile()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"using System;

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(int first, int second)
        {
            return first - second;
        }
    }
}");
            var input = new MutationTestInput()
            {
                ProjectInfo = new ProjectInfo(new MockFileSystem())
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "AssemblyName", "AssemblyName"},
                            { "TargetFileName", "TargetFileName.dll"},
                        }).Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult> { TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "AssemblyName", "TargetFileName"},
                        }).Object
                    }
                },
                AssemblyReferences = new List<PortableExecutableReference>() {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                }
            };
            var rollbackProcessMock = new Mock<IRollbackProcess>(MockBehavior.Strict);

            var target = new CsharpCompilingProcess(input, rollbackProcessMock.Object);

            using (var ms = new MemoryStream())
            {
                var result = target.Compile(new Collection<SyntaxTree>() { syntaxTree }, ms, null);
                result.Success.ShouldBe(true);
                ms.Length.ShouldBeGreaterThan(100, "No value was written to the MemoryStream by the compiler");
            }
        }

        [Fact]
        public void CompilingProcessTests_ShouldCallRollbackProcess_OnCompileError()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"using System;

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(string first, string second)
        {
            return first - second;
        }
    }
}");
            var input = new MutationTestInput()
            {
                ProjectInfo = new ProjectInfo(new MockFileSystem())
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "AssemblyName", "AssemblyName"},
                            { "TargetFileName", "TargetFileName.dll"},
                        }).Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult> { TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "AssemblyName", "TargetFileName"},
                        }).Object
                    }
                },
                AssemblyReferences = new List<PortableExecutableReference>() {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                }
            };
            var rollbackProcessMock = new Mock<IRollbackProcess>(MockBehavior.Strict);
            rollbackProcessMock.Setup(x => x.Start(It.IsAny<CSharpCompilation>(), It.IsAny<ImmutableArray<Diagnostic>>(), It.IsAny<bool>(), false))
                            .Returns((CSharpCompilation compilation, ImmutableArray<Diagnostic> diagnostics, bool _, bool _) =>
                            new RollbackProcessResult()
                            {
                                Compilation = compilation
                            });

            var target = new CsharpCompilingProcess(input, rollbackProcessMock.Object, new StrykerOptions());

            using (var ms = new MemoryStream())
            {
                Should.Throw<CompilationException>(() => target.Compile(new Collection<SyntaxTree>() { syntaxTree }, ms, null));
            }
            rollbackProcessMock.Verify(x => x.Start(It.IsAny<CSharpCompilation>(), It.IsAny<ImmutableArray<Diagnostic>>(), false, false),
                Times.AtLeast(2));
        }

        [Fact]
        public void CompilingProcessTests_ShouldOnlyRollbackErrors()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"using System;

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(int first, int second)
        {
            return first - second;
        }
    }
}");
            var input = new MutationTestInput()
            {
                ProjectInfo = new ProjectInfo(new MockFileSystem())
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "AssemblyName", "AssemblyName"},
                            { "TargetFileName", "TargetFileName.dll"},
                        }).Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult> { TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "AssemblyName", "TargetFileName"},
                        }).Object
                    }
                },
                AssemblyReferences = new List<PortableExecutableReference>() {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                }
            };
            var rollbackProcessMock = new Mock<IRollbackProcess>(MockBehavior.Strict);

            var target = new CsharpCompilingProcess(input, rollbackProcessMock.Object);

            using (var ms = new MemoryStream())
            {
                target.Compile(new Collection<SyntaxTree>() { syntaxTree }, ms, null);

                ms.Length.ShouldBeGreaterThan(100, "No value was written to the MemoryStream by the compiler");
            }
        }

        [Fact]
        public void CompilingProcessTests_SignedAssembliesMustBeSigned()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(int first, int second)
        {
            return first - second;
        }
    }
}");
            var input = new MutationTestInput()
            {
                ProjectInfo = new ProjectInfo(new MockFileSystem())
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "AssemblyName", "AssemblyName"},
                            { "TargetFileName", "TargetFileName.dll"},
                            { "SignAssembly", "true" },
                            { "AssemblyOriginatorKeyFile", Path.GetFullPath(Path.Combine("TestResources", "StrongNameKeyFile.snk")) }
                        },
                        projectFilePath: "TestResources").Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult> { TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "AssemblyName", "AssemblyName"},
                        }).Object
                    }
                },
                AssemblyReferences = new List<PortableExecutableReference>() {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                },

            };
            var rollbackProcessMock = new Mock<IRollbackProcess>(MockBehavior.Strict);

            var target = new CsharpCompilingProcess(input, rollbackProcessMock.Object);

            using (var ms = new MemoryStream())
            {
                var result = target.Compile(new Collection<SyntaxTree>() { syntaxTree }, ms, null);
                result.Success.ShouldBe(true);

                var key = Assembly.Load(ms.ToArray()).GetName().GetPublicKey();
                key.Length.ShouldBe(160, "Assembly was not signed");
                ms.Length.ShouldBeGreaterThan(100, "No value was written to the MemoryStream by the compiler");
            }
        }

        [Fact]
        public void CompilingProcessTests_ProperlyFailsWhenSigningKeyIsNotFound()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(int first, int second)
        {
            return first - second;
        }
    }
}");
            var input = new MutationTestInput()
            {
                ProjectInfo = new ProjectInfo(new MockFileSystem())
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "TargetFileName", "TargetFileName.dll"},
                            { "AssemblyName", "AssemblyName"},
                            { "SignAssembly", "true" },
                            { "AssemblyOriginatorKeyFile", "DoesNotExist.snk" }
                        },
                        projectFilePath: "project.csproj").Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult> { TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "AssemblyName", "AssemblyName"},
                        }).Object
                    }
                },
                AssemblyReferences = new List<PortableExecutableReference>() {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                },

            };
            var rollbackProcessMock = new Mock<IRollbackProcess>(MockBehavior.Strict);

            var target = new CsharpCompilingProcess(input, rollbackProcessMock.Object);

            using (var ms = new MemoryStream())
            {
                Should.Throw<CompilationException>(() => target.Compile(new Collection<SyntaxTree>() { syntaxTree }, ms, null));
            }
        }

        [Fact]
        public void CompilingProcessTests_MustIncludeVersionInfo()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"using System;

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(int first, int second)
        {
            return first - second;
        }
    }
}");
            var input = new MutationTestInput()
            {
                ProjectInfo = new ProjectInfo(new MockFileSystem())
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(
                        properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "TargetFileName", "TargetFileName.dll" },
                            { "AssemblyName", "AssemblyName"},
                        }).Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult> { TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "" },
                            { "TargetFileName", "TargetFileName.dll" },
                            { "AssemblyName", "AssemblyName"},
                        }).Object
                    }
                },
                AssemblyReferences = new List<PortableExecutableReference>() {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                }
            };
            var rollbackProcessMock = new Mock<IRollbackProcess>(MockBehavior.Strict);

            var target = new CsharpCompilingProcess(input, rollbackProcessMock.Object);

            using (var ms = new MemoryStream())
            {
                var result = target.Compile(new Collection<SyntaxTree>() { syntaxTree }, ms, null);
                result.Success.ShouldBe(true);

                Assembly.Load(ms.ToArray()).GetName().Version.ToString().ShouldBe("0.0.0.0");
            }
        }

        [Fact]
        public void ShouldCompileAndRollbackErrorWhenUninitializedVariable()
        {
            var sourceFile = @"using System;
using System.Collections.Generic;

namespace ExampleProject
{
    public class Calculator
    {
        public int Dummy()
        {
            int z;
            int y;
            if (true)
            {
                if (true)
                {
                   z = 1;
                   y = 0;
                }
                else
                {
                  z = 0;
                  y = 1;
                }
            }
            return z + y;
        }
    }
}";
            var projectContentsMutants = MutateAndCompileSource(sourceFile);
            // those results can change if mutators are added.
            projectContentsMutants.Count(t => t.ResultStatus == MutantStatus.CompileError).ShouldBe(9);
            projectContentsMutants.Count(t => t.ResultStatus == MutantStatus.NotRun).ShouldBe(0);
        }

        [Fact]
        public void ShouldCompileAndRollbackErrorsForEventHandler()
        {
            var sourceFile = @"using System;

namespace ExampleProject
{
    public class Calculator
    {
        public int Subtract(int first, int second)
        {
            return first - second;
        }
        private event Action SendCompleted;

        void TestMethod(){
            Action<Action> unsubscribe = (handler) => SendCompleted -= handler;
        }
    }
}";
            var projectContentsMutants = MutateAndCompileSource(sourceFile);
            // those results can change if mutators are added.
            projectContentsMutants.Count(t => t.ResultStatus == MutantStatus.CompileError).ShouldBe(1);
            projectContentsMutants.Count(t => t.ResultStatus == MutantStatus.NotRun).ShouldBe(3);
        }

        private static IEnumerable<Mutant> MutateAndCompileSource(string sourceFile)
        {
            var filesystemRoot = Path.GetPathRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            var inputFile = new CsharpFileLeaf()
            {
                SourceCode = sourceFile,
                SyntaxTree = CSharpSyntaxTree.ParseText(sourceFile)
            };
            var folder = new CsharpFolderComposite();
            folder.Add(inputFile);
            foreach (var (name, code) in CodeInjection.MutantHelpers)
            {
                folder.AddCompilationSyntaxTree(CSharpSyntaxTree.ParseText(code, path: name, encoding: Encoding.UTF32));
            }

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { Path.Combine(filesystemRoot, "ExampleProject", "Calculator.cs"), new MockFileData(sourceFile) },
                {
                    Path.Combine(filesystemRoot, "ExampleProject.Test", "bin", "Debug", "netcoreapp2.0", "ExampleProject.dll"),
                    new MockFileData("Bytecode")
                },
                {
                    Path.Combine(filesystemRoot, "ExampleProject.Test", "obj", "Release", "netcoreapp2.0",
                        "ExampleProject.dll"),
                    new MockFileData("Bytecode")
                }
            });

            var input = new MutationTestInput
            {
                ProjectInfo = new ProjectInfo(fileSystem)
                {
                    ProjectUnderTestAnalyzerResult = TestHelper.SetupProjectAnalyzerResult(
                        properties: new Dictionary<string, string>()
                        {
                            { "TargetDir", "Project" },
                            { "AssemblyName", "AssemblyName" },
                            { "TargetFileName", "TargetFileName.dll" },
                        }).Object,
                    TestProjectAnalyzerResults = new List<IAnalyzerResult>
                    {
                        TestHelper.SetupProjectAnalyzerResult(properties: new Dictionary<string, string>()
                        {
                            { "AssemblyName", "TargetFileName" },
                            { "TargetDir", "Test" },
                            { "TargetFileName", "TestTargetFileName.dll" },
                        }).Object
                    },
                    ProjectContents = folder
                },
                AssemblyReferences = new List<PortableExecutableReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                },
                TestRunner = new Mock<ITestRunner>(MockBehavior.Default).Object
            };

            var options = new StrykerOptions
            {
                MutationLevel = MutationLevel.Complete,
                OptimizationMode = OptimizationModes.CoverageBasedTest,
            };
            var process = new CsharpMutationProcess(input, fileSystem, options);
            process.Mutate();

            var projectContentsMutants = input.ProjectInfo.ProjectContents.Mutants;
            return projectContentsMutants;
        }
    }
}
