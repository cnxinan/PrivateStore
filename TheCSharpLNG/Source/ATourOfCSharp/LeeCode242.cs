/* 给定两个字符串 s 和 t ，编写一个函数来判断 t 是否是 s 的字母异位词。

注意：若 s 和 t 中每个字符出现的次数都相同，则称 s 和 t 互为字母异位词。

输入: s = "anagram", t = "nagaram"
输出: true

输入: s = "abc", t = "bcd"    
输出: false

思路：这个题目有可以用双循环，从s拿到1个字符，从t挪走一个，最后判断是否一样，但是这样复杂度是O平方；直接放数组计数是最
简单的实现；
错误思路：直接按ASCII 码加起来对比；从s里面那字符串到t里面indexof；
*/


using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATourOfCSharp
{
    [TestClass]
    public class LeeCode242
    {
        static bool isAnagram(string s, string t)
        {
            if (s.Length != t.Length)
            {
                return false;
            }

            int len = s.Length;
            int[] charArr = new int[26];

            for (int i = 0; i < len; i++)
            {
                charArr[s.ToCharArray()[i] - 'a']++;
                charArr[t.ToCharArray()[i] - 'a']--;
            }

            for (int i = 0; i < charArr.Length; i++)
            {
                if (charArr[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        [TestMethod]
        public void Test1()
        {
            Assert.AreEqual(isAnagram("anagram", "nagaram"), true);
            Assert.AreEqual(isAnagram("abc", "bcd"), false);
        }
    }
}
