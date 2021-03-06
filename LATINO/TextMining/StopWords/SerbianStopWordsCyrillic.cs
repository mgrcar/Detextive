﻿/*==========================================================================;
 *
 *  This file is part of LATINO. See http://www.latinolib.org
 *
 *  File:    SerbianStopWordsCyrillic.cs
 *  Desc:    Serbian stop words (cyrillic)
 *  Created: Apr-2010
 *
 *  Author:  Miha Grcar
 *
 ***************************************************************************/

namespace Latino.TextMining
{
    /* .-----------------------------------------------------------------------
       |
       |  Class StopWords
       |
       '-----------------------------------------------------------------------
    */
    public static partial class StopWords
    {
        // this list is transliterated from http://www.filewatcher.com/p/punbb-1.2.14.tbz.550363/www/punbb/upload/lang/Serbian/stopwords.txt.html
        public static Set<string>.ReadOnly SerbianStopWordsCyrillic
            = new Set<string>.ReadOnly(new Set<string>(new string[] {
                "баш",
                "без",
                "биће",
                "био",
                "бити",
                "близу",
                "број",
                "дана",
                "данас",
                "доћи",
                "добар",
                "добити",
                "док",
                "доле",
                "дошао",
                "други",
                "дуж",
                "два",
                "често",
                "чији",
                "где",
                "горе",
                "хвала",
                "ићи",
                "иако",
                "иде",
                "има",
                "имам",
                "имао",
                "испод",
                "између",
                "изнад",
                "изван",
                "изволи",
                "један",
                "једини",
                "једном",
                "јесте",
                "још",
                "јуче",
                "кад",
                "како",
                "као",
                "кога",
                "која",
                "које",
                "који",
                "кроз",
                "мали",
                "мањи",
                "мисли",
                "много",
                "моћи",
                "могу",
                "мора",
                "морао",
                "наћи",
                "наш",
                "негде",
                "него",
                "некад",
                "неки",
                "немам",
                "нешто",
                "није",
                "ниједан",
                "никада",
                "нисмо",
                "ништа",
                "њега",
                "његов",
                "њен",
                "њих",
                "њихов",
                "око",
                "около",
                "она",
                "онај",
                "они",
                "оно",
                "осим",
                "остали",
                "отишао",
                "овако",
                "овамо",
                "овде",
                "ове",
                "ово",
                "питати",
                "почетак",
                "поједини",
                "после",
                "поводом",
                "правити",
                "пре",
                "преко",
                "према",
                "први",
                "пут",
                "радије",
                "сада",
                "смети",
                "шта",
                "ствар",
                "стварно",
                "сутра",
                "сваки",
                "све",
                "свим",
                "свугде",
                "тачно",
                "тада",
                "тај",
                "такође",
                "тамо",
                "тим",
                "учинио",
                "учинити",
                "умало",
                "унутра",
                "употребити",
                "узети",
                "ваш",
                "већина",
                "веома",
                "видео",
                "више",
                "захвалити",
                "зашто",
                "због",
                "желео",
                "жели",
                "знати"}));
    }
}
