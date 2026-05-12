using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Utilities;

// Ported verbatim from FRB1's FlatRedBall.Utilities.StringFunctions; only the
// MakeStringUnique overloads (and the GetNumberAtEnd/IncrementNumberAtEnd
// helpers they depend on) are kept.
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

    private static int GetNumberAtEnd(string stringToGetNumberFrom)
    {
        int letterChecking = stringToGetNumberFrom.Length;
        do
        {
            letterChecking--;
        } while (letterChecking > -1 && char.IsDigit(stringToGetNumberFrom[letterChecking]));

        if (letterChecking == stringToGetNumberFrom.Length - 1 && !char.IsDigit(stringToGetNumberFrom[letterChecking]))
        {
            throw new ArgumentException("The argument string has no number at the end.");
        }

        return Convert.ToInt32(
            stringToGetNumberFrom.Substring(letterChecking + 1, stringToGetNumberFrom.Length - letterChecking - 1));
    }

    private static string IncrementNumberAtEnd(string originalString)
    {
        if (string.IsNullOrEmpty(originalString))
        {
            return "1";
        }

        int letterChecking = originalString.Length;
        do
        {
            letterChecking--;
        } while (letterChecking > -1 && char.IsDigit(originalString[letterChecking]));

        if (letterChecking == originalString.Length - 1 && !char.IsDigit(originalString[letterChecking]))
        {
            originalString = originalString + 1.ToString();
            return originalString;
        }
        string numAtEnd = originalString.Substring(letterChecking + 1, originalString.Length - letterChecking - 1);
        string baseString = originalString.Remove(letterChecking + 1, originalString.Length - letterChecking - 1);
        int numAtEndAsInt = Convert.ToInt32(numAtEnd);
        numAtEndAsInt++;
        return baseString + numAtEndAsInt.ToString().PadLeft(numAtEnd.Length, '0');
    }
}
