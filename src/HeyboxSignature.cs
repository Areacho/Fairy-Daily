// C# adaptation of Cansiny0320/heybox-daily, MIT. See HEYBOX-LICENSE.txt.
// Pinned protocol: APP 1.3.398 / build 1142 / shader 14.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MiyouDaily
{
    public static class HeyboxSignature
    {
        static readonly ulong[] K = {
  0x428a2f98d728ae22UL,
  0x7137449123ef65cdUL,
  0xb5c0fbcfec4d3b2fUL,
  0xe9b5dba58189dbbcUL,
  0x3956c25bf348b538UL,
  0x59f111f1b605d019UL,
  0x923f82a4af194f9bUL,
  0xab1c5ed5da6d8118UL,
  0xd807aa98a3030242UL,
  0x12835b0145706fbeUL,
  0x243185be4ee4b28cUL,
  0x550c7dc3d5ffb4e2UL,
  0x72be5d74f27b896fUL,
  0x80deb1fe3b1696b1UL,
  0x9bdc06a725c71235UL,
  0xc19bf174cf692694UL,
  0xe49b69c19ef14ad2UL,
  0xefbe4786384f25e3UL,
  0xfc19dc68b8cd5b5UL,
  0x240ca1cc77ac9c65UL,
  0x2de92c6f592b0275UL,
  0x4a7484aa6ea6e483UL,
  0x5cb0a9dcbd41fbd4UL,
  0x76f988da831153b5UL,
  0x983e5152ee66dfabUL,
  0xa831c66d2db43210UL,
  0xb00327c898fb213fUL,
  0xbf597fc7beef0ee4UL,
  0xc6e00bf33da88fc2UL,
  0xd5a79147930aa725UL,
  0x6ca6351e003826fUL,
  0x142929670a0e6e70UL,
  0x27b70a8546d22ffcUL,
  0x2e1b21385c26c926UL,
  0x4d2c6dfc5ac42aedUL,
  0x53380d139d95b3dfUL,
  0x650a73548baf63deUL,
  0x766a0abb3c77b2a8UL,
  0x81c2c92e47edaee6UL,
  0x92722c851482353bUL,
  0xa2bfe8a14cf10364UL,
  0xa81a664bbc423001UL,
  0xc24b8b70d0f89791UL,
  0xc76c51a30654be30UL,
  0xd192e819d6ef5218UL,
  0xd69906245565a910UL,
  0xf40e35855771202aUL,
  0x106aa07032bbd1b8UL,
  0x19a4c116b8d2d0c8UL,
  0x1e376c085141ab53UL,
  0x2748774cdf8eeb99UL,
  0x34b0bcb5e19b48a8UL,
  0x391c0cb3c5c95a63UL,
  0x4ed8aa4ae3418acbUL,
  0x5b9cca4f7763e373UL,
  0x682e6ff3d6b2b8a3UL,
  0x748f82ee5defb2fcUL,
  0x78a5636f43172f60UL,
  0x84c87814a1f0ab72UL,
  0x8cc702081a6439ecUL,
  0x7ba0ea2d98160007UL,
  0x7eabf2d0c21f964aUL,
  0x8dbe8d038b409545UL,
  0x90bb1721582e8285UL,
  0x99a2ad45936d4e61UL,
  0x9f86e289fe03e73aUL,
  0xa84c4472faa9a82fUL,
  0xb3df34fce89e0532UL,
  0xb99bb8d7b173534fUL,
  0xbc76cbab1aea1f9cUL,
  0xc226a69a780f3cc3UL,
  0xd304f19aa233957dUL,
  0xde1be20a212129ddUL,
  0xe39bb43755141950UL,
  0xee84927cea48ddd2UL,
  0xf3edd2773c523b67UL,
  0xfbfdfe53a8d32f2aUL,
  0xbee2c7ab77e9e25UL,
  0xe90181cf1b09e56UL,
  0x25f57204c725bed8UL,

        };
        static readonly ulong[] Initial = {0x6a09e667f3bcc908UL,0xbb67ae8584caa73bUL,0x3c6ef372fe94f82bUL,0xa54ff53a5f1d36f1UL,0x510e527fade682d1UL,0x9b05688c2b3e6c1fUL,0x1f83d9abfb41bd6bUL,0x5be0cd19137e2179UL};
        static ulong R(ulong x,int n){return (x>>n)|(x<<(64-n));}
        static ulong Read(byte[] input,int offset){ulong value=0;for(int i=0;i<8;i++)value=(value<<8)|input[offset+i];return value;}
        static void Write(byte[] output,int offset,ulong value){for(int i=7;i>=0;i--){output[offset+i]=(byte)value;value>>=8;}}
        static byte[] Digest(byte[] input)
        {
            int size=((input.Length+17+127)/128)*128;var padded=new byte[size];
            Buffer.BlockCopy(input,0,padded,0,input.Length);padded[input.Length]=128;Write(padded,size-8,(ulong)input.Length*8);
            var state=(ulong[])Initial.Clone();
            unchecked
            {
                for(int offset=0;offset<size;offset+=128)
                {
                    var w=new ulong[80];for(int i=0;i<16;i++)w[i]=Read(padded,offset+i*8);
                    for(int i=16;i<80;i++){ulong x=w[i-2],y=w[i-15];w[i]=(R(x,38)^R(x,55)^(x>>6))+w[i-7]+(R(y,1)^R(y,8)^(y>>7))+w[i-16];}
                    ulong a=state[0],b=state[1],c=state[2],d=state[3],e=state[4],f=state[5],g=state[6],h=state[7];
                    for(int i=0;i<80;i++)
                    {
                        ulong t1=h+(R(e,14)^R(e,18)^R(e,41))+((e&(f^g))^f)+K[i]+w[i];
                        ulong t2=(R(a,21)^R(a,31)^R(a,39))+((a&(b|c))|(b&c));
                        h=g;g=f;f=e;e=d+t1;d=c;c=b;b=a;a=t1+t2;
                    }
                    ulong[] v={a,b,c,d,e,f,g,h};for(int i=0;i<8;i++)state[i]+=v[i];
                }
            }
            var result=new byte[64];for(int i=0;i<8;i++)Write(result,i*8,state[i]);return result;
        }
        static byte[] Join(params byte[][] pieces){return pieces.SelectMany(x=>x).ToArray();}
        static byte[] Hmac(byte[] key,byte[] data)
        {
            if(key.Length>128)key=Digest(key);var inner=Enumerable.Repeat((byte)0x52,128).ToArray();var outer=Enumerable.Repeat((byte)0x6c,128).ToArray();
            for(int i=0;i<key.Length;i++){inner[i]^=key[i];outer[i]^=key[i];}
            return Digest(Join(outer,Digest(Join(inner,data))));
        }
        static byte[] Transform(string input,bool twice)
        {
            byte[] raw=Encoding.UTF8.GetBytes(input);int nul=Array.IndexOf(raw,(byte)0);int length=Math.Min(127,nul<0?raw.Length:nul);var output=new byte[length];
            for(int i=0;i<length;i++){int c=raw[i],x=(c&127)^0x37;output[i]=(byte)(x==0||(i+1)%3==0?((c&7)<<4)^0x37:x);}
            if(twice)for(int i=0;i<length;i++){int c=output[i],x=(c&127)^0x46;output[i]=(byte)(x==0||(i+1)%5==0?((c&31)<<2)^0x46:x);}
            return output;
        }
        static uint Crc(byte[] bytes,uint polynomial)
        {
            uint crc=uint.MaxValue;foreach(byte value in bytes){crc^=value;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==0?0:polynomial);}return crc^uint.MaxValue;
        }
        public static string Nonce(string time,Func<int,int> indexSource=null)
        {
            var date=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(long.Parse(time,CultureInfo.InvariantCulture));int day=date.Year*10000+date.Month*100+date.Day;
            const string all="abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";var alphabet=new char[32];for(int i=0;i<32;i++)alphabet[i]=all[(day+705+i*7)%62];
            var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
            var result=new char[32];for(int i=0;i<32;i++)result[i]=alphabet[indexSource==null?(bytes[i]&31):indexSource(32)];return new string(result);
        }
        public static Dictionary<string,string> Sign(string path,string time,string debugInfo,string osVersion,string version)
        {
            byte[] standard;using(var hmac=new HMACSHA512(Encoding.ASCII.GetBytes("LFEJGJJOKCEHODNMNFIKKONFBKIHJFKB")))standard=hmac.ComputeHash(Encoding.UTF8.GetBytes(path+time+debugInfo));
            string hash=BitConverter.ToString(Hmac(Transform(path+time,false),Transform(debugInfo+osVersion+version,true))).Replace("-","").ToLowerInvariant();
            return new Dictionary<string,string>{{"_time",time},{"nonce",Nonce(time)},{"hkey",Crc(standard,0x88a4b5a0).ToString("X",CultureInfo.InvariantCulture)},{"_rnd","14:"+Crc(Encoding.ASCII.GetBytes(hash),0xad350290).ToString("X",CultureInfo.InvariantCulture)}};
        }
        public static string NormalizePath(string path){return "/"+string.Join("/",path.Split('/').Where(x=>x.Length>0))+"/";}
    }
}
