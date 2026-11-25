using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace AnimationEditor.Core.Utilities;

// Ported from FRB1's FlatRedBall.Utilities.StringFunctions; only the
// MakeStringUnique overloads (and the helpers they depend on) are kept.
public static class StringFunctions
{
    public static string MakeStringUnique(string stringToMakeUnique, List<string> stringList)
        => MakeStringUnique(stringToMakeUnique, stringList, 1);

    public static string MakeStringUnique(string stringToMakeUnique, List<string> stringList, int numberToStartAt)
    {
        for (int i = 0; i < stringList.Count; i++)
        {
            if (stringToMakeUnique == stringList[i])
            {
                stringToMakeUnique = IncrementNumberAtEnd(stringToMakeUnique);

                while (GetNumberAtEnd(stringToMakeUnique) < numberToStartAt)
                {
                    stringToMakeUnique = IncrementNumberAtEnd(stringToMakeUnique);
                }

                i = -1;
            }
        }

        return stringToMakeUnique;
    }

    private static BigInteger GetNumberAtEnd(string name) =>
        BigInteger.Parse(name.AsSpan(NumberStart(name)), NumberStyles.None, CultureInfo.InvariantCulture);

    private static string IncrementNumberAtEnd(string name)
    {
        int start = NumberStart(name);
        if (start == name.Length) return name + "1";
        string number = (GetNumberAtEnd(name) + 1).ToString(CultureInfo.InvariantCulture);
        return name[..start] + number.PadLeft(name.Length - start, '0');
    }

    // Only 0-9 count as a number, of any length.
    private static int NumberStart(string name)
    {
        int start = name.Length;
        while (start > 0 && char.IsAsciiDigit(name[start - 1])) start--;
        return start;
    }
}
