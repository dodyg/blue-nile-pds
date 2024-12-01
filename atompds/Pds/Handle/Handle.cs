using System.Text.RegularExpressions;
using atompds.Pds.Config;
using Identity;
using Xrpc;

namespace atompds.Pds.Handle;

public partial class Handle
{
    private readonly IdentityConfig _identityConfig;
    private readonly HandleResolver _handleResolver;
    public Handle(IdentityConfig identityConfig, HandleResolver handleResolver)
    {
        _identityConfig = identityConfig;
        _handleResolver = handleResolver;
    }
    
    public async Task<string> NormalizeAndValidateHandle(string inputHandle, string? did, bool? allowReserved)
    {
        var handle = BaseNormalizeAndValidate(inputHandle);
        
        if (!IsValidTld(handle))
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle TLD is invalid or disallowed"));
        }
        
        if (HasExplicitSlur(handle))
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Inappropriate language in handle"));
        }
        
        if (IsServiceDomain(handle, _identityConfig.ServiceHandleDomains.ToArray()))
        {
            EnsureHandleServiceConstraints(handle, _identityConfig.ServiceHandleDomains.ToArray(), allowReserved ?? false);
        }
        else 
        {
            if (did == null)
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Not a supported handle domain"));
            }
            
            var resolvedDid = await _handleResolver.Resolve(handle, CancellationToken.None);
            if (resolvedDid == null || resolvedDid != did)
            {
                throw new XRPCError(new InvalidHandleErrorDetail("External handle did not resolve to DID"));
            }
        }
        
        return handle;
    }

    private void EnsureHandleServiceConstraints(string handle, string[] availableUserDomains, bool allowReserved = false)
    {
        var supportedDomain = availableUserDomains.FirstOrDefault(handle.EndsWith) ?? "";
        var front = handle[..^supportedDomain.Length];
        if (front.Contains('.'))
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Invalid characters in handle"));
        }
        
        if (front.Length < 3)
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle too short"));
        }
        
        if (front.Length > 18)
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle too long"));
        }
        
        if (!allowReserved && Reserved.ReservedSubdomains.Any(x => x == front))
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Reserved handle"));
        }
    }
    
    private bool IsServiceDomain(string handle, string[] serviceDomains)
    {
        return serviceDomains.Any(handle.EndsWith);
    }

    private bool IsValidTld(string tld)
    {
        return !DISALLOWED_TLDS.Contains(tld);
    }

    private static readonly Regex[] Sr =
    [
        ES1(),
        ES2(),
        ES3(),
        ES4(),
        ES5(),
        ES6(),
        ES7()
    ];
    
    private bool HasExplicitSlur(string handle)
    {
        var sh = handle.Replace(".", "").Replace("-", "").Replace("_", "");
        return Sr.Any(r => r.IsMatch(handle) || r.IsMatch(sh));
    }
    
    private string BaseNormalizeAndValidate(string handle)
    {
        var normalized = NormalizeHandle(handle);
        EnsureValidHandle(normalized);
        return normalized;
    }

    private string NormalizeHandle(string handle)
    {
        return handle.ToLower();
    }

    // Handle constraints, in English:
    //  - must be a possible domain name
    //    - RFC-1035 is commonly referenced, but has been updated. eg, RFC-3696,
    //      section 2. and RFC-3986, section 3. can now have leading numbers (eg,
    //      4chan.org)
    //    - "labels" (sub-names) are made of ASCII letters, digits, hyphens
    //    - can not start or end with a hyphen
    //    - TLD (last component) should not start with a digit
    //    - can't end with a hyphen (can end with digit)
    //    - each segment must be between 1 and 63 characters (not including any periods)
    //    - overall length can't be more than 253 characters
    //    - separated by (ASCII) periods; does not start or end with period
    //    - case insensitive
    //    - domains (handles) are equal if they are the same lower-case
    //    - punycode allowed for internationalization
    //  - no whitespace, null bytes, joining chars, etc
    //  - does not validate whether domain or TLD exists, or is a reserved or
    //    special TLD (eg, .onion or .local)
    //  - does not validate punycode
    private void EnsureValidHandle(string handle)
    {
        if (!BasicHandleRegex().IsMatch(handle))
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Disallowed characters in handle (ASCII letters, digits, dashes, periods only)"));
        }

        if (handle.Length > 253)
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle is too long (253 chars max)"));
        }

        var labels = handle.Split('.');
        if (labels.Length < 2)
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle domain needs at least two parts"));
        }

        for (var i = 0; i < labels.Length; i++)
        {
            var l = labels[i];
            if (l.Length < 1)
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle parts can not be empty"));
            }
            if (l.Length > 63)
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle part too long (max 63 chars)"));
            }
            if (l.EndsWith('-') || l.StartsWith('-'))
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle parts can not start or end with hyphens"));
            }
            if (i + 1 == labels.Length && !Regex.IsMatch(l, "^[a-zA-Z]"))
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle final component (TLD) must start with ASCII letter"));
            }
        }
    }
    
    public static readonly string[] DISALLOWED_TLDS =
    [
        ".local",
        ".arpa",
        ".invalid",
        ".localhost",
        ".internal",
        ".example",
        ".alt",
        // policy could concievably change on ".onion" some day
        ".onion",
        // NOTE: .test is allowed in testing and devopment. In practical terms
        // "should" "never" actually resolve and get registered in production
    ];


    [GeneratedRegex("^[a-zA-Z0-9.-]*$")]
    private static partial Regex BasicHandleRegex();

    [GeneratedRegex(
        "\b[cĆćĈĉČčĊċÇçḈḉȻȼꞒꞓꟄꞔƇƈɕ][hĤĥȞȟḦḧḢḣḨḩḤḥḪḫH\u0331ẖĦħⱧⱨꞪɦꞕΗНн][iÍíi\u0307\u0301Ììi\u0307\u0300ĬĭÎîǏǐÏïḮḯĨĩi\u0307\u0303ĮįĮ\u0301į\u0307\u0301Į\u0303į\u0307\u0303ĪīĪ\u0300ī\u0300ỈỉȈȉI\u030bi\u030bȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl\u0303ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌ][nŃńǸǹŇňÑñṄṅŅņṆṇṊṋṈṉN\u0308n\u0308ƝɲŊŋꞐꞑꞤꞥᵰᶇɳȵꬻꬼИиПпＮｎ][kḰḱǨǩĶķḲḳḴḵƘƙⱩⱪᶄꝀꝁꝂꝃꝄꝅꞢꞣ][sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS\u0329s\u0329ꞨꞩⱾȿꟅʂᶊᵴ]?\b")]
    private static partial Regex ES1();
    [GeneratedRegex(
        "\b[cĆćĈĉČčĊċÇçḈḉȻȼꞒꞓꟄꞔƇƈɕ][ÓóÒòŎŏÔôỐốỒồỖỗỔổǑǒÖöȪȫŐőÕõṌṍṎṏȬȭȮȯO\u0358o\u0358ȰȱØøǾǿǪǫǬǭŌōṒṓṐṑỎỏȌȍȎȏƠơỚớỜờỠỡỞởỢợỌọỘộO\u0329o\u0329Ò\u0329ò\u0329Ó\u0329ó\u0329ƟɵꝊꝋꝌꝍⱺＯｏ0]{2}[nŃńǸǹŇňÑñṄṅŅņṆṇṊṋṈṉN\u0308n\u0308ƝɲŊŋꞐꞑꞤꞥᵰᶇɳȵꬻꬼИиПпＮｎ][sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS\u0329s\u0329ꞨꞩⱾȿꟅʂᶊᵴ]?\b")]
    private static partial Regex ES2();
    [GeneratedRegex(
        "\b[fḞḟƑƒꞘꞙᵮᶂ][aÁáÀàĂăẮắẰằẴẵẲẳÂâẤấẦầẪẫẨẩǍǎÅåǺǻÄäǞǟÃãȦȧǠǡĄąĄ\u0301ą\u0301Ą\u0303ą\u0303ĀāĀ\u0300ā\u0300ẢảȀȁA\u030ba\u030bȂȃẠạẶặẬậḀḁȺⱥꞺꞻᶏẚＡａ@4][gǴǵĞğĜĝǦǧĠġG\u0303g\u0303ĢģḠḡǤǥꞠꞡƓɠᶃꬶＧｇqꝖꝗꝘꝙɋʠ]{1,2}([ÓóÒòŎŏÔôỐốỒồỖỗỔổǑǒÖöȪȫŐőÕõṌṍṎṏȬȭȮȯO\u0358o\u0358ȰȱØøǾǿǪǫǬǭŌōṒṓṐṑỎỏȌȍȎȏƠơỚớỜờỠỡỞởỢợỌọỘộO\u0329o\u0329Ò\u0329ò\u0329Ó\u0329ó\u0329ƟɵꝊꝋꝌꝍⱺＯｏ0e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ\u0304ê\u0304Ê\u030cê\u030cĚěËëẼẽĖėĖ\u0301ė\u0301Ė\u0303ė\u0303ȨȩḜḝĘęĘ\u0301ę\u0301Ę\u0303ę\u0303ĒēḖḗḔḕẺẻȄȅE\u030be\u030bȆȇẸẹỆệḘḙḚḛɆɇE\u0329e\u0329È\u0329è\u0329É\u0329é\u0329ᶒⱸꬴꬳＥｅiÍíi\u0307\u0301Ììi\u0307\u0300ĬĭÎîǏǐÏïḮḯĨĩi\u0307\u0303ĮįĮ\u0301į\u0307\u0301Į\u0303į\u0307\u0303ĪīĪ\u0300ī\u0300ỈỉȈȉI\u030bi\u030bȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl\u0303ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌ][tŤťṪṫŢţṬṭȚțṰṱṮṯŦŧȾⱦƬƭƮʈT\u0308ẗᵵƫȶ]{1,2}([rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR\u0303r\u0303ɌɍꞦꞧⱤɽᵲᶉꭉ][yÝýỲỳŶŷY\u030aẙŸÿỸỹẎẏȲȳỶỷỴỵɎɏƳƴỾỿ]|[rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR\u0303r\u0303ɌɍꞦꞧⱤɽᵲᶉꭉ][iÍíi\u0307\u0301Ììi\u0307\u0300ĬĭÎîǏǐÏïḮḯĨĩi\u0307\u0303ĮįĮ\u0301į\u0307\u0301Į\u0303į\u0307\u0303ĪīĪ\u0300ī\u0300ỈỉȈȉI\u030bi\u030bȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl\u0303ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌ][e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ\u0304ê\u0304Ê\u030cê\u030cĚěËëẼẽĖėĖ\u0301ė\u0301Ė\u0303ė\u0303ȨȩḜḝĘęĘ\u0301ę\u0301Ę\u0303ę\u0303ĒēḖḗḔḕẺẻȄȅE\u030be\u030bȆȇẸẹỆệḘḙḚḛɆɇE\u0329e\u0329È\u0329è\u0329É\u0329é\u0329ᶒⱸꬴꬳＥｅ])?)?[sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS\u0329s\u0329ꞨꞩⱾȿꟅʂᶊᵴ]?\b")]
    private static partial Regex ES3();
    [GeneratedRegex(
        "\b[kḰḱǨǩĶķḲḳḴḵƘƙⱩⱪᶄꝀꝁꝂꝃꝄꝅꞢꞣ][iÍíi̇́Ììi̇̀ĬĭÎîǏǐÏïḮḯĨĩi̇̃ĮįĮ́į̇́Į̃į̇̃ĪīĪ̀ī̀ỈỉȈȉI̋i̋ȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl̃ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌyÝýỲỳŶŷY̊ẙŸÿỸỹẎẏȲȳỶỷỴỵɎɏƳƴỾỿ][kḰḱǨǩĶķḲḳḴḵƘƙⱩⱪᶄꝀꝁꝂꝃꝄꝅꞢꞣ][e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ]([rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR̃r̃ɌɍꞦꞧⱤɽᵲᶉꭉ][yÝýỲỳŶŷY̊ẙŸÿỸỹẎẏȲȳỶỷỴỵɎɏƳƴỾỿ]|[rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR̃r̃ɌɍꞦꞧⱤɽᵲᶉꭉ][iÍíi̇́Ììi̇̀ĬĭÎîǏǐÏïḮḯĨĩi̇̃ĮįĮ́į̇́Į̃į̇̃ĪīĪ̀ī̀ỈỉȈȉI̋i̋ȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl̃ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌ][e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ])?[sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS̩s̩ꞨꞩⱾȿꟅʂᶊᵴ]*\b")]
    private static partial Regex ES4();
    [GeneratedRegex(
        "\b[nŃńǸǹŇňÑñṄṅŅņṆṇṊṋṈṉN̈n̈ƝɲŊŋꞐꞑꞤꞥᵰᶇɳȵꬻꬼИиПпＮｎ][iÍíi̇́Ììi̇̀ĬĭÎîǏǐÏïḮḯĨĩi̇̃ĮįĮ́į̇́Į̃į̇̃ĪīĪ̀ī̀ỈỉȈȉI̋i̋ȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl̃ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌoÓóÒòŎŏÔôỐốỒồỖỗỔổǑǒÖöȪȫŐőÕõṌṍṎṏȬȭȮȯO͘o͘ȰȱØøǾǿǪǫǬǭŌōṒṓṐṑỎỏȌȍȎȏƠơỚớỜờỠỡỞởỢợỌọỘộO̩o̩Ò̩ò̩Ó̩ó̩ƟɵꝊꝋꝌꝍⱺＯｏІіa4ÁáÀàĂăẮắẰằẴẵẲẳÂâẤấẦầẪẫẨẩǍǎÅåǺǻÄäǞǟÃãȦȧǠǡĄąĄ́ą́Ą̃ą̃ĀāĀ̀ā̀ẢảȀȁA̋a̋ȂȃẠạẶặẬậḀḁȺⱥꞺꞻᶏẚＡａ][gǴǵĞğĜĝǦǧĠġG̃g̃ĢģḠḡǤǥꞠꞡƓɠᶃꬶＧｇqꝖꝗꝘꝙɋʠ]{2}(l[e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ]t|[e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅaÁáÀàĂăẮắẰằẴẵẲẳÂâẤấẦầẪẫẨẩǍǎÅåǺǻÄäǞǟÃãȦȧǠǡĄąĄ́ą́Ą̃ą̃ĀāĀ̀ā̀ẢảȀȁA̋a̋ȂȃẠạẶặẬậḀḁȺⱥꞺꞻᶏẚＡａ][rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR̃r̃ɌɍꞦꞧⱤɽᵲᶉꭉ]?|n[ÓóÒòŎŏÔôỐốỒồỖỗỔổǑǒÖöȪȫŐőÕõṌṍṎṏȬȭȮȯO͘o͘ȰȱØøǾǿǪǫǬǭŌōṒṓṐṑỎỏȌȍȎȏƠơỚớỜờỠỡỞởỢợỌọỘộO̩o̩Ò̩ò̩Ó̩ó̩ƟɵꝊꝋꝌꝍⱺＯｏ0][gǴǵĞğĜĝǦǧĠġG̃g̃ĢģḠḡǤǥꞠꞡƓɠᶃꬶＧｇqꝖꝗꝘꝙɋʠ]|[a4ÁáÀàĂăẮắẰằẴẵẲẳÂâẤấẦầẪẫẨẩǍǎÅåǺǻÄäǞǟÃãȦȧǠǡĄąĄ́ą́Ą̃ą̃ĀāĀ̀ā̀ẢảȀȁA̋a̋ȂȃẠạẶặẬậḀḁȺⱥꞺꞻᶏẚＡａ]?)?[sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS̩s̩ꞨꞩⱾȿꟅʂᶊᵴ]?\b")]
    private static partial Regex ES5();
    [GeneratedRegex(
        "[nŃńǸǹŇňÑñṄṅŅņṆṇṊṋṈṉN̈n̈ƝɲŊŋꞐꞑꞤꞥᵰᶇɳȵꬻꬼИиПпＮｎ][iÍíi̇́Ììi̇̀ĬĭÎîǏǐÏïḮḯĨĩi̇̃ĮįĮ́į̇́Į̃į̇̃ĪīĪ̀ī̀ỈỉȈȉI̋i̋ȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl̃ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌoÓóÒòŎŏÔôỐốỒồỖỗỔổǑǒÖöȪȫŐőÕõṌṍṎṏȬȭȮȯO͘o͘ȰȱØøǾǿǪǫǬǭŌōṒṓṐṑỎỏȌȍȎȏƠơỚớỜờỠỡỞởỢợỌọỘộO̩o̩Ò̩ò̩Ó̩ó̩ƟɵꝊꝋꝌꝍⱺＯｏІіa4ÁáÀàĂăẮắẰằẴẵẲẳÂâẤấẦầẪẫẨẩǍǎÅåǺǻÄäǞǟÃãȦȧǠǡĄąĄ́ą́Ą̃ą̃ĀāĀ̀ā̀ẢảȀȁA̋a̋ȂȃẠạẶặẬậḀḁȺⱥꞺꞻᶏẚＡａ][gǴǵĞğĜĝǦǧĠġG̃g̃ĢģḠḡǤǥꞠꞡƓɠᶃꬶＧｇqꝖꝗꝘꝙɋʠ]{2}(l[e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ]t|[e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ][rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR̃r̃ɌɍꞦꞧⱤɽᵲᶉꭉ])[sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS̩s̩ꞨꞩⱾȿꟅʂᶊᵴ]?")]
    private static partial Regex ES6();
    [GeneratedRegex(
        "\b[tŤťṪṫŢţṬṭȚțṰṱṮṯŦŧȾⱦƬƭƮʈT̈ẗᵵƫȶ][rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR̃r̃ɌɍꞦꞧⱤɽᵲᶉꭉ][aÁáÀàĂăẮắẰằẴẵẲẳÂâẤấẦầẪẫẨẩǍǎÅåǺǻÄäǞǟÃãȦȧǠǡĄąĄ́ą́Ą̃ą̃ĀāĀ̀ā̀ẢảȀȁA̋a̋ȂȃẠạẶặẬậḀḁȺⱥꞺꞻᶏẚＡａ4]+[nŃńǸǹŇňÑñṄṅŅņṆṇṊṋṈṉN̈n̈ƝɲŊŋꞐꞑꞤꞥᵰᶇɳȵꬻꬼИиПпＮｎ]{1,2}([iÍíi̇́Ììi̇̀ĬĭÎîǏǐÏïḮḯĨĩi̇̃ĮįĮ́į̇́Į̃į̇̃ĪīĪ̀ī̀ỈỉȈȉI̋i̋ȊȋỊịꞼꞽḬḭƗɨᶖİiIıＩｉ1lĺľļḷḹl̃ḽḻłŀƚꝉⱡɫɬꞎꬷꬸꬹᶅɭȴＬｌ][e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ]|[yÝýỲỳŶŷY̊ẙŸÿỸỹẎẏȲȳỶỷỴỵɎɏƳƴỾỿ]|[e3ЄєЕеÉéÈèĔĕÊêẾếỀềỄễỂểÊ̄ê̄Ê̌ê̌ĚěËëẼẽĖėĖ́ė́Ė̃ė̃ȨȩḜḝĘęĘ́ę́Ę̃ę̃ĒēḖḗḔḕẺẻȄȅE̋e̋ȆȇẸẹỆệḘḙḚḛɆɇE̩e̩È̩è̩É̩é̩ᶒⱸꬴꬳＥｅ][rŔŕŘřṘṙŖŗȐȑȒȓṚṛṜṝṞṟR̃r̃ɌɍꞦꞧⱤɽᵲᶉꭉ])[sŚśṤṥŜŝŠšṦṧṠṡŞşṢṣṨṩȘșS̩s̩ꞨꞩⱾȿꟅʂᶊᵴ]?\b")]
    private static partial Regex ES7();
}