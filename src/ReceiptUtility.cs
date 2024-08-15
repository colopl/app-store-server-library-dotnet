using System.Formats.Asn1;
using System.Numerics;

namespace AppStoreServerLibraryDotnet;

public class ReceiptUtility
{
    /// <summary>
    /// Parses a receipt and returns the transaction id.
    /// Using this transaction id, you should use the App Store Server API to retrieve the subscription status.
    /// </summary>
    /// <param name="appReceipt">App Store Receipt</param>
    /// <returns>The transaction id of the receipt</returns>
    /// <exception cref="FormatException"></exception>
    public string ExtractTransactionIdFromAppReceipt(string appReceipt)
    {
        /*
         * The receipt is a PKCS#7 signed data structure. Encoded in base64.
         * The point of this method is to parse the receipt by following the ASN.1 structure and extract the transaction id.
         * Unfortunately I didn't find any documentation about the exacte structure of the receipt.
         * I read the official implementations for other languages and tried to replicate the same logic.
         */
        string transactionId = string.Empty;

        byte[] decodedReceipts = Convert.FromBase64String(appReceipt);

        AsnReader reader = new(decodedReceipts, AsnEncodingRules.BER);

        Asn1Tag tag = reader.PeekTag();
        if (tag.TagClass != TagClass.Universal || tag.TagValue != (int) UniversalTagNumber.Sequence)
        {
            throw new FormatException("Expected ASN.1 Sequence.");
        }

        AsnReader sequence = reader.ReadSequence();
        tag = sequence.PeekTag();
        if (tag.TagClass != TagClass.Universal || tag.TagValue != (int) UniversalTagNumber.ObjectIdentifier ||
            sequence.ReadObjectIdentifier() != "1.2.840.113549.1.7.2")
        {
            throw new FormatException("Expected PKCS#7 Object.");
        }

        var contextSpecificClass = new Asn1Tag(TagClass.ContextSpecific, 0);
        if (!sequence.HasData && !sequence.PeekTag().HasSameClassAndValue(contextSpecificClass))
        {
            throw new FormatException("Root Context-specific class is missing.");
        }

        AsnReader contextSequence = sequence.ReadSequence(contextSpecificClass);
        AsnReader subSequence = contextSequence.ReadSequence();
        // Reads the int 1
        subSequence.ReadInteger();
        // Reads Set with sequence with oid 2.16.840.1.101.3.4.2.1
        subSequence.ReadSetOf();
        //Read Sequence with oid 1.2.840.113549.1.7.1
        subSequence = subSequence.ReadSequence();
        subSequence.ReadObjectIdentifier();

        //Get sequence from the tagged context-specific class
        AsnReader nextSequence = subSequence.ReadSequence(contextSpecificClass);
        //Read the DER Octet String containing the receipt
        byte[] outerReceiptOctetString = nextSequence.ReadOctetString();

        AsnReader outerReceiptReader = new(outerReceiptOctetString, AsnEncodingRules.BER);

        if(!outerReceiptReader.HasData)
        {
            throw new FormatException("Outer receipt sequence is empty.");
        }

        AsnReader outerReceiptSets = outerReceiptReader.ReadSetOf();

        byte[] inAppPurchasesBytes = [];
        int IN_APP_TYPE_ID = 17;

        //Parse the sequences contained in the outer receipt until the one with the in-app purchases type id is found
        do
        {
            /*
             * The outer receipt contains a sequence of sequences, each containing a type id, a version, and a content string.
             * Set
                   Sequence
                       Integer(20)
                       Integer(1)
                       DER Octet String[2]
                   Sequence
                       Integer(25)
                       Integer(1)
                       DER Octet String[3]
                   ...
             */

            AsnReader outerReceiptSequences = outerReceiptSets.ReadSequence();
            BigInteger typeId = outerReceiptSequences.ReadInteger();
            outerReceiptSequences.ReadInteger();
            byte[] contentString = outerReceiptSequences.ReadOctetString();

            if (typeId == IN_APP_TYPE_ID)
            {
                inAppPurchasesBytes = contentString;
                break;
            }
        } while (outerReceiptSets.HasData);

        //Parse the in-app purchases sequences to find the transaction id
        int TRANSACTION_IDENTIFIER_TYPE_ID = 1703;

        AsnReader inAppPurchasesReader = new(inAppPurchasesBytes, AsnEncodingRules.BER);

        if(!inAppPurchasesReader.HasData)
        {
            throw new FormatException("In App Purchase sequence is empty.");
        }

        AsnReader inAppPurchasesSets = inAppPurchasesReader.ReadSetOf();

        do
        {
            /* The in-app purchases sequence contains a set of sequences, each containing a type id, a version, and a content string.
             * Set
                   Sequence
                       Integer(1709)
                       Integer(1)
                       DER Octet String[2]
                    ...
             *
             */
            AsnReader inAppPurchasesSequence = inAppPurchasesSets.ReadSequence();
            BigInteger typeId = inAppPurchasesSequence.ReadInteger();
            inAppPurchasesSequence.ReadInteger();
            byte[] contentString = inAppPurchasesSequence.ReadOctetString();


            if (typeId == TRANSACTION_IDENTIFIER_TYPE_ID)
            {
                var transactionReader = new AsnReader(contentString, AsnEncodingRules.BER);

                transactionId = transactionReader.ReadCharacterString(UniversalTagNumber.UTF8String);
                break;
            }
        } while (inAppPurchasesSets.HasData);

        return transactionId;
    }

    /* For reference here is a visual representation of the receipt structure
     * ASN.1 structure of the receipt data
     *
     * Sequence
       ObjectIdentifier(1.2.840.113549.1.7.2)
       Tagged [CONTEXT 0]
           Sequence
               Integer(1)
               Set
                   Sequence
                       ObjectIdentifier(2.16.840.1.101.3.4.2.1)
                       NULL
               Sequence
                   ObjectIdentifier(1.2.840.113549.1.7.1)
                   Tagged [CONTEXT 0]
Purchases here ====>DER Octet String[945]
               Tagged [CONTEXT 0] IMPLICIT
                   Sequence
                       Sequence
                           Sequence
                               Tagged [CONTEXT 0]
                                   Integer(2)
                               Integer(29116449735413815654132084939298878297)
                               Sequence
                                   ObjectIdentifier(1.2.840.113549.1.1.11)
                                   NULL
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           UTF8String(Apple Worldwide Developer Relations Certification Authority)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           UTF8String(G5)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           UTF8String(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                               Sequence
                                   UTCTime(220902191357Z)
                                   UTCTime(241001191356Z)
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           UTF8String(Mac App Store and iTunes Store Receipt Signing)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           UTF8String(Apple Worldwide Developer Relations)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           UTF8String(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                               Sequence
                                   Sequence
                                       ObjectIdentifier(1.2.840.113549.1.1.1)
                                       NULL
                                   DER Bit String[270, 0]
                               Tagged [CONTEXT 3]
                                   Sequence
                                       Sequence
                                           ObjectIdentifier(2.5.29.19)
                                           Boolean(True)
                                           DER Octet String[2]
                                       Sequence
                                           ObjectIdentifier(2.5.29.35)
                                           DER Octet String[24]
                                       Sequence
                                           ObjectIdentifier(1.3.6.1.5.5.7.1.1)
                                           DER Octet String[100]
                                       Sequence
                                           ObjectIdentifier(2.5.29.32)
                                           DER Octet String[278]
                                       Sequence
                                           ObjectIdentifier(2.5.29.31)
                                           DER Octet String[41]
                                       Sequence
                                           ObjectIdentifier(2.5.29.14)
                                           DER Octet String[22]
                                       Sequence
                                           ObjectIdentifier(2.5.29.15)
                                           Boolean(True)
                                           DER Octet String[4]
                                       Sequence
                                           ObjectIdentifier(1.2.840.113635.100.6.11.1)
                                           DER Octet String[2]
                           Sequence
                               ObjectIdentifier(1.2.840.113549.1.1.11)
                               NULL
                           DER Bit String[256, 0]
                       Sequence
                           Sequence
                               Tagged [CONTEXT 0]
                                   Integer(2)
                               Integer(339651503466496215992526486017692226778114910612)
                               Sequence
                                   ObjectIdentifier(1.2.840.113549.1.1.11)
                                   NULL
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           PrintableString(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           PrintableString(Apple Certification Authority)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           PrintableString(Apple Root CA)
                               Sequence
                                   UTCTime(201216193856Z)
                                   UTCTime(301210000000Z)
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           UTF8String(Apple Worldwide Developer Relations Certification Authority)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           UTF8String(G5)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           UTF8String(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                               Sequence
                                   Sequence
                                       ObjectIdentifier(1.2.840.113549.1.1.1)
                                       NULL
                                   DER Bit String[270, 0]
                               Tagged [CONTEXT 3]
                                   Sequence
                                       Sequence
                                           ObjectIdentifier(2.5.29.19)
                                           Boolean(True)
                                           DER Octet String[8]
                                       Sequence
                                           ObjectIdentifier(2.5.29.35)
                                           DER Octet String[24]
                                       Sequence
                                           ObjectIdentifier(1.3.6.1.5.5.7.1.1)
                                           DER Octet String[56]
                                       Sequence
                                           ObjectIdentifier(2.5.29.31)
                                           DER Octet String[39]
                                       Sequence
                                           ObjectIdentifier(2.5.29.14)
                                           DER Octet String[22]
                                       Sequence
                                           ObjectIdentifier(2.5.29.15)
                                           Boolean(True)
                                           DER Octet String[4]
                                       Sequence
                                           ObjectIdentifier(1.2.840.113635.100.6.2.1)
                                           DER Octet String[2]
                           Sequence
                               ObjectIdentifier(1.2.840.113549.1.1.11)
                               NULL
                           DER Bit String[256, 0]
                       Sequence
                           Sequence
                               Tagged [CONTEXT 0]
                                   Integer(2)
                               Integer(2)
                               Sequence
                                   ObjectIdentifier(1.2.840.113549.1.1.5)
                                   NULL
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           PrintableString(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           PrintableString(Apple Certification Authority)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           PrintableString(Apple Root CA)
                               Sequence
                                   UTCTime(060425214036Z)
                                   UTCTime(350209214036Z)
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           PrintableString(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           PrintableString(Apple Certification Authority)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           PrintableString(Apple Root CA)
                               Sequence
                                   Sequence
                                       ObjectIdentifier(1.2.840.113549.1.1.1)
                                       NULL
                                   DER Bit String[270, 0]
                               Tagged [CONTEXT 3]
                                   Sequence
                                       Sequence
                                           ObjectIdentifier(2.5.29.15)
                                           Boolean(True)
                                           DER Octet String[4]
                                       Sequence
                                           ObjectIdentifier(2.5.29.19)
                                           Boolean(True)
                                           DER Octet String[5]
                                       Sequence
                                           ObjectIdentifier(2.5.29.14)
                                           DER Octet String[22]
                                       Sequence
                                           ObjectIdentifier(2.5.29.35)
                                           DER Octet String[24]
                                       Sequence
                                           ObjectIdentifier(2.5.29.32)
                                           DER Octet String[264]
                           Sequence
                               ObjectIdentifier(1.2.840.113549.1.1.5)
                               NULL
                           DER Bit String[256, 0]
               Set
                   Sequence
                       Integer(1)
                       Sequence
                           Sequence
                               Set
                                   Sequence
                                       ObjectIdentifier(2.5.4.3)
                                       UTF8String(Apple Worldwide Developer Relations Certification Authority)
                               Set
                                   Sequence
                                       ObjectIdentifier(2.5.4.11)
                                       UTF8String(G5)
                               Set
                                   Sequence
                                       ObjectIdentifier(2.5.4.10)
                                       UTF8String(Apple Inc.)
                               Set
                                   Sequence
                                       ObjectIdentifier(2.5.4.6)
                                       PrintableString(US)
                           Integer(29116449735413815654132084939298878297)
                       Sequence
                           ObjectIdentifier(2.16.840.1.101.3.4.2.1)
                           NULL
                       Sequence
                           ObjectIdentifier(1.2.840.113549.1.1.1)
                           NULL
                       DER Octet String[256]

                Set
                       Sequence
                           Integer(1)
                           Sequence
                               Sequence
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.3)
                                           UTF8String(Apple Worldwide Developer Relations Certification Authority)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.11)
                                           UTF8String(G5)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.10)
                                           UTF8String(Apple Inc.)
                                   Set
                                       Sequence
                                           ObjectIdentifier(2.5.4.6)
                                           PrintableString(US)
                               Integer(29116449735413815654132084939298878297)
                           Sequence
                               ObjectIdentifier(2.16.840.1.101.3.4.2.1)
                               NULL
                           Sequence
                               ObjectIdentifier(1.2.840.113549.1.1.1)
                               NULL
                           DER Octet String[256]


Outer receipt Structure :

       Set
           Sequence
               Integer(20)
               Integer(1)
               DER Octet String[2]
           Sequence
               Integer(25)
               Integer(1)
               DER Octet String[3]
           Sequence
               Integer(10)
               Integer(1)
               DER Octet String[4]
           Sequence
               Integer(14)
               Integer(1)
               DER Octet String[4]
           Sequence
               Integer(11)
               Integer(1)
               DER Octet String[5]
           Sequence
               Integer(13)
               Integer(1)
               DER Octet String[5]
           Sequence
               Integer(1)
               Integer(1)
               DER Octet String[6]
           Sequence
               Integer(9)
               Integer(1)
               DER Octet String[6]
           Sequence
               Integer(16)
               Integer(1)
               DER Octet String[6]
           Sequence
               Integer(19)
               Integer(1)
               DER Octet String[6]
           Sequence
               Integer(3)
               Integer(1)
               DER Octet String[7]
           Sequence
               Integer(15)
               Integer(1)
               DER Octet String[8]
           Sequence
               Integer(0)
               Integer(1)
               DER Octet String[12]
           Sequence
               Integer(4)
               Integer(2)
               DER Octet String[16]
           Sequence
               Integer(2)
               Integer(1)
               DER Octet String[18]
           Sequence
               Integer(5)
               Integer(1)
               DER Octet String[20]
           Sequence
               Integer(8)
               Integer(1)
               DER Octet String[22]
           Sequence
               Integer(12)
               Integer(1)
               DER Octet String[22]
           Sequence
               Integer(18)
               Integer(1)
               DER Octet String[22]
           Sequence
               Integer(7)
               Integer(1)
               DER Octet String[39]
           Sequence
               Integer(6)
               Integer(1)
               DER Octet String[71]
           Sequence
               Integer(17)
               Integer(1)
               DER Octet String[413]

In App Purchases Structure :
Set
       Sequence
           Integer(1709)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1712)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1714)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1715)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1716)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1717)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1718)
           Integer(1)
           DER Octet String[2]
       Sequence
           Integer(1701)
           Integer(1)
           DER Octet String[3]
       Sequence
           Integer(1707)
           Integer(1)
           DER Octet String[3]
       Sequence
           Integer(1713)
           Integer(1)
           DER Octet String[3]
       Sequence
           Integer(1719)
           Integer(1)
           DER Octet String[3]
       Sequence
           Integer(1722)
           Integer(1)
           DER Octet String[3]
       Sequence
           Integer(1710)
           Integer(1)
           DER Octet String[7]
       Sequence
           Integer(1711)
           Integer(1)
           DER Octet String[9]
       Sequence
           Integer(1703)
           Integer(1)
           DER Octet String[17]
       Sequence
           Integer(1705)
           Integer(1)
           DER Octet String[17]
       Sequence
           Integer(1704)
           Integer(1)
           DER Octet String[22]
       Sequence
           Integer(1706)
           Integer(1)
           DER Octet String[22]
       Sequence
           Integer(1708)
           Integer(1)
           DER Octet String[22]
       Sequence
           Integer(1702)
           Integer(1)
           DER Octet String[44]

     *
     */
}