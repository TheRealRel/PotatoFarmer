using System; using System.IO; using PotatoFarmer;
static class T {
  static int fails;
  static void Eq(object a, object b, string what){ if (!Equals(a,b)) { fails++; Console.WriteLine("FAIL "+what+": "+a+" != "+b);} }
  static int Main(){
    var s = Stock.Parse("Raspberry:12;Carrot:5"); Eq(s.Total,17,"total"); Eq(s.Count("Carrot"),5,"carrot");
    s.Add("Carrot",3); Eq(s.Format(),"Raspberry:12;Carrot:8","add keeps order");
    Eq(s.Remove("Raspberry",12),true,"remove all"); Eq(s.Format(),"Carrot:8","entry gone");
    Eq(s.Remove("Carrot",9),false,"too many"); Eq(s.Remove("Blueberries",1),false,"missing");
    s.Add("bad;name",1); s.Add("bad:name",1); s.Add("",1); s.Add("Neg",-1); Eq(s.Format(),"Carrot:8","bad input ignored");
    Eq(Stock.Parse("").Total,0,"empty"); Eq(Stock.Parse("x;Raspberry:abc;:3;Carrot:2").Format(),"Carrot:2","junk skipped");
    Eq(Stock.Parse("Big:2147483647;Big:5").Count("Big"),int.MaxValue,"no overflow");
    // mesh file header and sizes
    using (var r = new BinaryReader(File.OpenRead("assets/trough.mesh"))) {
      Eq(new string(r.ReadChars(4)),"PTM1","magic"); int v=r.ReadInt32(), i=r.ReadInt32();
      Eq(r.BaseStream.Length, 12L + v*(12+12+8) + i*4L, "file size"); Eq(i%3,0,"triangles");
      r.BaseStream.Seek(12L + v*32L, SeekOrigin.Begin); int max=0; for(int k=0;k<i;k++){int x=r.ReadInt32(); if(x>max)max=x; if(x<0){fails++;}} Eq(max<v,true,"indices in range");
    }
    Console.WriteLine(fails==0?"all passed":fails+" failed"); return fails;
  }
}
