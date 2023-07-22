var nerdle0 = new Nerdle()
{
    Slot = new (char?, char[]?)[5]
    {
        (null, "".ToCharArray()),
        (null, "".ToCharArray()),
        (null, "".ToCharArray()),
        (null, "".ToCharArray()),
        (null, "".ToCharArray()),
    },
    Symbols = new (char c, int qty, int min)[]
    {
        ('A', 0, 0),
        ('B', 0, 0),
        ('C', 0, 0),
        ('D', 0, 0),
        ('E', 0, 0),
        ('F', 0, 0),
        ('G', 0, 0),
        ('H', 0, 0),
        ('I', 0, 0),
        ('J', 0, 0),
        ('K', 0, 0),
        ('L', 0, 0),
        ('M', 0, 0),
        ('N', 0, 0),
        ('O', 0, 0),
        ('P', 0, 0),
        ('Q', 0, 0),
        ('R', 0, 0),
        ('S', 0, 0),
        ('T', 0, 0),
        ('U', 0, 0),
        ('V', 0, 0),
        ('W', 0, 0),
        ('X', 0, 0),
        ('Y', 0, 0),
        ('Z', 0, 0),
    },
}
.WithProbalities(NerdleProbalistic.CreateMarkovChain(File.ReadAllLines(@"liste.de.mots.francais.frgut.txt").ToHashSet()))
// .WithProbalities(NerdleProbalistic.CreateMarkovChain(File.ReadAllLines(@"enable1.txt").ToHashSet()))
.GetAllLines(printMaxCombinatory: true, steps: 200);

#if RELEASE
foreach (var line in nerdle0)
    Console.WriteLine(line);
#endif
