# Round 12, batch G46b: NetDevR7Test has no System.Linq: the silenced-clip checks use Enumerable.All by its full name.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevR7Test.cs'
Rep 'hand.All(c => c && c.length > 0.15f && c.length < 0.45f) && rifleShots.All(c => c && c.length > 0.15f && c.length < 0.45f)' 'System.Linq.Enumerable.All(hand, c => c && c.length > 0.15f && c.length < 0.45f) && System.Linq.Enumerable.All(rifleShots, c => c && c.length > 0.15f && c.length < 0.45f)'
Save-Src
