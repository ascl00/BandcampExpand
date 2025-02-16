using Microsoft.VisualStudio.TestTools.UnitTesting;
using BandcampExpand;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.IO;

namespace BandcampExpand.Tests
{
    [TestClass()]
    public class ProgramTests
    {
        [TestMethod()]
        public void StripIfStartsWithTest()
        {
            Assert.AreEqual("foo", Program.StripIfStartsWith("bah - foo", "bah - "));
            Assert.AreEqual("bah - foo", Program.StripIfStartsWith("bah - bah - foo", "bah - "));
            Assert.AreEqual("foo", Program.StripIfStartsWith("foo", "bah - "));
        }

        [TestMethod()]
        public void GetPathForFileTypeTest()
        {
            string str = BandcampExpand.Program.GetPathForFileType(new FileInfo("bah"), "foo");
            Console.WriteLine(str);
            Assert.Fail();
        }
    }
}