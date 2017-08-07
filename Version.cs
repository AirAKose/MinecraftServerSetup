using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftServerSetup
{
    public class Version: IReadOnlyList<int>, IComparable<Version>, IComparable<int[]>, IEquatable<Version>, IEquatable<int[]>
    {
        int[] numbers;
        public Version(string versionString)
        {
            int result = 0;
            numbers = (from nums in versionString.Split('.', '_', ',')
                       where !string.IsNullOrEmpty(nums) && int.TryParse(nums, out result)
                       select result).ToArray();
        }
        public Version(params int[] nums)
        {
            numbers = nums;
        }

        public int this[int index]
        {
            get
            {
                return numbers[index];
            }
        }

        public int Count
        {
            get
            {
                return numbers.Length;
            }
        }

        public int CompareTo(Version other)
        {
            return this.CompareTo(other.numbers);
        }
        public int CompareTo(params int[] versionArr)
        {
            var maxLength = Math.Max(numbers.Length, versionArr.Length);

            for (var i = 0; i < maxLength; ++i)
            {
                if (this[i] > versionArr[i])
                    return 1;
                else if (this[i] < versionArr[i])
                    return -1;
            }

            return numbers.Length - versionArr.Length;
        }

        public bool Equals(Version other)
        {
            return CompareTo(other.numbers) == 0;
        }

        public bool Equals(int[] versionArr)
        {
            return CompareTo(versionArr) == 0;
        }

        public IEnumerator<int> GetEnumerator()
        {
            foreach(int number in numbers)
            {
                yield return number;
            }
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        public static Version FindFromPathEnd(string path)
        {
            var end = path.Length;
            var start = path.Length - 1;
            for(;start>=0;--start )
            {
                char c = path[start];
                if(char.IsNumber(c) || c == '_' || c == '.')
                {
                    if (end == path.Length)
                        end = start;
                }
                else if(end != path.Length)
                {
                    break;
                }
            }
            if (end == path.Length) return null;

            return new Version(path.Substring(start + 1, end - start));
        }

    }
}
