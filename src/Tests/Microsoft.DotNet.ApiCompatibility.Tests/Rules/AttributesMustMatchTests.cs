﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class AttributesMustMatchTests
    {
        /*
         * Tests for:
         * - Types
         * - Fields
         * - Properties
         * - Methods
         * - Events
         * - ReturnValues
         * - Constructors
         * - Generic Parameters
         * 
         * Grouped into:
         * - Type
         * - Member
         */

        public static TheoryData<string, string, CompatDifference[]> TypesCases => new()
        {
            // No change to type's attributes
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
new CompatDifference[] {}
            },
            // Attribute removed from type
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "T:CompatTests.First:[T:System.SerializableAttribute]")
}
            },
            // Attribute changed on type
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = false, B = 4)]
  public class First {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "T:CompatTests.First:[T:CompatTests.FooAttribute]")
}
            },
            // Attribute added to type
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "T:CompatTests.First:[T:System.SerializableAttribute]")
}
            }
        };

        public static TheoryData<string, string, CompatDifference[]> MembersCases => new() {
            // Attributes on method
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [Foo(""S"", A = true, B = 3)]
    [Bar]
    public void F() {}
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [Foo(""T"")]
    [Baz]
    public void F() {}
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "M:CompatTests.First.F:[T:CompatTests.FooAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "M:CompatTests.First.F:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "M:CompatTests.First.F:[T:CompatTests.BazAttribute]")
}
            },
            // Attributes on property
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [Foo(""S"", A = true, B = 3)]
    [Bar]
    public int F { get; }
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [Foo(""T"")]
    [Baz]
    public int F { get; }
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "P:CompatTests.First.F:[T:CompatTests.FooAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "P:CompatTests.First.F:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "P:CompatTests.First.F:[T:CompatTests.BazAttribute]")
}
            },
            // Attributes on event
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    public delegate void EventHandler(object sender, object e);

    [Foo(""S"", A = true, B = 3)]
    [Bar]
    public event EventHandler F;
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    public delegate void EventHandler(object sender, object e);

    [Foo(""T"")]
    [Baz]
    public event EventHandler F;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "E:CompatTests.First.F:[T:CompatTests.FooAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "E:CompatTests.First.F:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "E:CompatTests.First.F:[T:CompatTests.BazAttribute]")
}
            },
            // Attributes on constructor
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [Foo(""S"", A = true, B = 3)]
    [Bar]
    public First() {}
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [Foo(""T"")]
    [Baz]
    public First() {}
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "M:CompatTests.First.#ctor:[T:CompatTests.FooAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "M:CompatTests.First.#ctor:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "M:CompatTests.First.#ctor:[T:CompatTests.BazAttribute]")
}
            },
            // Attributes on return type
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [return: Foo(""S"", A = true, B = 3)]
    [return: Bar]
    public int F() => 0;
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    [return: Foo(""T"")]
    [return: Baz]
    public int F() => 0;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "M:CompatTests.First.F->int:[T:CompatTests.FooAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "M:CompatTests.First.F->int:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "M:CompatTests.First.F->int:[T:CompatTests.BazAttribute]")
}
            },
            // Attributes on method parameter
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    public void F([Bar] int v, [Foo(""S"", A = true, B = 0)] string s) {}
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    public void F([Baz] int v, [Foo(""T"")] string s) {}
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "M:CompatTests.First.F(System.Int32,System.String)$0:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "M:CompatTests.First.F(System.Int32,System.String)$0:[T:CompatTests.BazAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "M:CompatTests.First.F(System.Int32,System.String)$1:[T:CompatTests.FooAttribute]"),

}
            },
            // Attributes on type parameter of class
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First<[Bar] T1, [Foo(""S"", A = true, B = 0)] T2> {}
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First<[Baz] T1, [Foo(""T"")] T2> {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "T:CompatTests.First`2<0>:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "T:CompatTests.First`2<0>:[T:CompatTests.BazAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "T:CompatTests.First`2<1>:[T:CompatTests.FooAttribute]"),

}
            },
            // Attributes on type parameter of method
            {
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    public void F<[Bar] T1, [Foo(""S"", A = true, B = 0)] T2>() {}
  }
}
",
                @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A = false;
    public int B = 0;
  }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BarAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class BazAttribute : Attribute { }

  public class First {

    public void F<[Baz] T1, [Foo(""T"")] T2>() {}
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, "", DifferenceType.Removed, "M:CompatTests.First.F``2<0>:[T:CompatTests.BarAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, "", DifferenceType.Added, "M:CompatTests.First.F``2<0>:[T:CompatTests.BazAttribute]"),
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, "", DifferenceType.Changed, "M:CompatTests.First.F``2<1>:[T:CompatTests.FooAttribute]"),

}
            }
        };

        [Theory]
        [MemberData(nameof(TypesCases))]
        [MemberData(nameof(MembersCases))]
        public void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            using TempDirectory root = new();
            string filePath = Path.Combine(root.DirPath, "exclusions.txt");
            File.Create(filePath).Dispose();
            TestRuleFactory s_ruleFactory = new((settings, context) => new AttributesMustMatch(settings, context, new[] { filePath }));
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);
            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestExclusionsFilteredOut()
        {
            using TempDirectory root = new();
            string filePath = Path.Combine(root.DirPath, "exclusions.txt");
            File.WriteAllText(filePath, "T:System.SerializableAttribute");
            TestRuleFactory s_ruleFactory = new((settings, context) => new AttributesMustMatch(settings, context, new[] { filePath }));
            string leftSyntax = @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
";
            string rightSyntax = @"
namespace CompatTests
{
  using System;
  
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  public class FooAttribute : Attribute {
    public FooAttribute(String s) {}
    public bool A;
    public int B;
  }

  [Serializable]
  [Foo(""S"", A = true, B = 3)]
  public class First {}
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);
            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);
            Assert.Empty(actual);
        }
    }
}
