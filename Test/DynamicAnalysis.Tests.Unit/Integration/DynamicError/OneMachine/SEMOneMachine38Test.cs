﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine38Test.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.DynamicAnalysis.Tests.Unit
{
    [TestClass]
    public class SEMOneMachine38Test : BasePSharpTest
    {
        /// <summary>
        /// P# semantics test: one machine: "null" handler semantics.
        /// Testing that null handler is inherited by the pushed state.
        /// </summary>
        [TestMethod]
        public void TestNullHandlerInheritedByPushTransition()
        {
            var test = @"
using System;
using Microsoft.PSharp;

namespace SystematicTesting
{
    class E : Event { }

    class Program : Machine
    {
        int i;

        [Start]
        [OnEntry(nameof(EntryInit))]
        [OnExit(nameof(ExitInit))]
        [OnEventPushState(typeof(E), typeof(Call))]
        [OnEventDoAction(typeof(Default), nameof(InitAction))]
        class Init : MachineState { }

        void EntryInit()
        {
            i = 0;
            this.Raise(new E());
        }

        void ExitInit() { }

        void InitAction()
        {
            this.Assert(false); // reachable
        }

        [OnEntry(nameof(EntryCall))]
        [OnExit(nameof(ExitCall))]
        [IgnoreEvents(typeof(E))]
        class Call : MachineState { }

        void EntryCall()
        {
            if (i == 0)
            {
                this.Raise(new E());
            }
            else
            {
                i = i + 1;
            }
        }

        void ExitCall() { }
    }

    public static class TestProgram
    {
        public static void Main(string[] args)
        {
            TestProgram.Execute();
            Console.ReadLine();
        }

        [Test]
        public static void Execute()
        {
            PSharpRuntime.CreateMachine(typeof(Program));
        }
    }
}";

            var parser = new CSharpParser(new PSharpProject(), SyntaxFactory.ParseSyntaxTree(test), true);
            var program = parser.Parse();
            program.Rewrite();

            Configuration.SuppressTrace = true;
            Configuration.Verbose = 2;

            var assembly = base.GetAssembly(program.GetSyntaxTree());
            AnalysisContext.Create(assembly);

            SCTEngine.Setup();
            SCTEngine.Run();

            Assert.AreEqual(1, SCTEngine.NumOfFoundBugs);
        }
    }
}
