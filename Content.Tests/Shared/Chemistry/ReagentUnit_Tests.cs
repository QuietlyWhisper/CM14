﻿using Content.Shared.Chemistry;
using NUnit.Framework;
using System;

namespace Content.Tests.Shared.Chemistry
{
    [TestFixture, TestOf(typeof(ReagentUnit))]
    public class ReagentUnit_Tests
    {
        [Test]
        [TestCase(1, "1")]
        [TestCase(0, "0")]
        [TestCase(-1, "-1")]
        public void ReagentUnitIntegerTests(int value, string expected)
        {
            var result = ReagentUnit.New(value);
            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(1.001f, "1")]
        [TestCase(0.999f, "1")]
        public void ReagentUnitFloatTests(float value, string expected)
        {
            var result = ReagentUnit.New(value);
            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(1.001d, "1")]
        [TestCase(0.999d, "1")]
        public void ReagentUnitDoubleTests(double value, string expected)
        {
            var result = ReagentUnit.New(value);
            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase("1.001", "1")]
        [TestCase("0.999", "1")]
        public void ReagentUnitDecimalTests(string valueAsString, string expected)
        {
            var value = decimal.Parse(valueAsString);
            var result = ReagentUnit.New(value);
            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(1.001f, 1.001f, "2")]
        [TestCase(1.001f, 1.004f, "2")]
        [TestCase(1f, 2.005f, "3.01")]
        public void CalculusPlus(float aFloat, float bFloat, string expected)
        {
            var a = ReagentUnit.New(aFloat);
            var b = ReagentUnit.New(bFloat);

            var result = a + b;

            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(1.001f, 1.001f, "0")]
        [TestCase(1.001f, 1.004f, "0")]
        [TestCase(1f, 2.005f, "-1.01")]
        public void CalculusMinus(float aFloat, float bFloat, string expected)
        {
            var a = ReagentUnit.New(aFloat);
            var b = ReagentUnit.New(bFloat);

            var result = a - b;

            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(1.001f, 3f, "0.33")]
        [TestCase(0.999f, 3f, "0.33")]
        [TestCase(2.1f, 3f, "0.7")]
        public void CalculusDivision(float aFloat, float bFloat, string expected)
        {
            var a = ReagentUnit.New(aFloat);
            var b = ReagentUnit.New(bFloat);

            var result = a / b;

            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(1.001f, 0.999f, "1")]
        [TestCase(0.999f, 3f, "3")]
        public void CalculusMultiplication(float aFloat, float bFloat, string expected)
        {
            var a = ReagentUnit.New(aFloat);
            var b = ReagentUnit.New(bFloat);

            var result = a * b;

            Assert.AreEqual(expected, $"{result}");
        }

        [Test]
        [TestCase(0.995f, 100)]
        [TestCase(1.005f, 100)]
        [TestCase(2.005f, 201)]
        public void FloatRoundingTest(float a, int expected)
        {
            var result = (int) Math.Round(a * (float) Math.Pow(10, 2));
            Assert.AreEqual(expected, result);
        }
    }
}
