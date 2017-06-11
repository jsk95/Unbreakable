﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Unbreakable.Tests {
    public static class TestHelper {
        public delegate object Invoke(params object[] args);

        public static MemoryStream Compile(string code) {
            var compilation = CSharpCompilation.Create(
                "_",
                new[] { CSharpSyntaxTree.ParseText(code) },
                new[] {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Stack<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join("\r\n", result.Diagnostics));
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static Invoke RewriteAndGetMethodWrappedInScope(string code, string typeName, string methodName) {
            var assemblySourceStream = Compile(code);
            var assemblyTargetStream = new MemoryStream();

            var token = AssemblyGuard.Rewrite(assemblySourceStream, assemblyTargetStream);

            return args => {
                using (token.Scope(new RuntimeGuardSettings { TimeLimit = TimeSpan.FromMilliseconds(100) })) {
                    var method = GetInstanceMethod(assemblyTargetStream, typeName, methodName);
                    return method(args);
                }
            };
        }

        private static Invoke GetInstanceMethod(MemoryStream assemblyStream, string typeName, string methodName) {
            var assembly = Assembly.Load(assemblyStream.ToArray());
            var type = assembly.GetType(typeName);
            var instance = Activator.CreateInstance(type);
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            return args => method.Invoke(instance, args);
        }
    }
}