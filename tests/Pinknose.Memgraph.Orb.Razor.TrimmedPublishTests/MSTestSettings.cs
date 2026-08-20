// Serial on purpose: every test in this assembly shares one published app and one browser,
// and the publish itself is the expensive part -- there is nothing to gain from overlapping
// a handful of page loads against it.
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel, Workers = 1)]
