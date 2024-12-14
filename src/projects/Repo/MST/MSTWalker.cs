namespace Repo.MST;

public interface IWalkerStatus
{
    public bool Done { get; }
}

public record WalkerStatusDone : IWalkerStatus
{
    public bool Done => true;
}

public class WalkerStatusProgress : IWalkerStatus
{
    public required INodeEntry Current;
    public int Index;
    public MST? Walking; // set to null if `Current` is the root of the tree
    public bool Done => false;
}

public class MSTWalker
{
    private readonly Stack<IWalkerStatus> _stack = new();
    public readonly MST Root;
    public IWalkerStatus Status;

    public MSTWalker(MST root)
    {
        Root = root;
        Status = new WalkerStatusProgress {Current = root, Walking = null, Index = 0};
    }

    public int Layer()
    {
        if (Status is WalkerStatusDone)
        {
            throw new InvalidOperationException("Walker is done");
        }

        var progress = (WalkerStatusProgress)Status;

        if (progress.Walking != null)
        {
            return progress.Walking.Layer ?? 0;
        }

        if (progress.Current is MST mst)
        {
            return (mst.Layer ?? 0) + 1;
        }

        throw new InvalidOperationException("Could not identify layer of walk");
    }


    // move to the next node in the subtree, skipping over the subtree
    public async Task StepOver()
    {
        while (true)
        {
            if (Status is WalkerStatusDone)
            {
                return;
            }

            var progress = (WalkerStatusProgress)Status;
            // if stepping over the root of the node, we're done
            if (progress.Walking == null)
            {
                Status = new WalkerStatusDone();
                return;
            }

            var entries = await progress.Walking.GetEntries();
            progress.Index++;
            var next = entries.Length > progress.Index ? entries[progress.Index] : null;
            if (next == null)
            {
                if (!_stack.TryPop(out var popped))
                {
                    Status = new WalkerStatusDone();
                }
                else
                {
                    Status = popped;
                    continue;
                }
            }
            else
            {
                progress.Current = next;
            }
            break;
        }
    }

    public async Task StepInto()
    {
        if (Status is WalkerStatusDone)
        {
            return;
        }

        var progress = (WalkerStatusProgress)Status;
        if (progress.Walking == null)
        {
            if (progress.Current is not MST curr)
            {
                throw new InvalidOperationException("The root of the tree is not an MST");
            }

            var next = await curr.AtIndex(0);
            if (next == null)
            {
                Status = new WalkerStatusDone();
            }
            else
            {
                Status = new WalkerStatusProgress {Current = next, Walking = curr, Index = 0};
            }
        }
        else
        {
            if (progress.Current is not MST curr)
            {
                throw new InvalidOperationException("No tree at pointer, cannot step into");
            }

            var next = await curr.AtIndex(0);
            if (next == null)
            {
                throw new InvalidOperationException("Tried to step into a node with no children");
            }

            _stack.Push(Status);
            progress.Walking = curr;
            progress.Current = next;
            progress.Index = 0;
        }
    }


    // advance the pointer to the next node in the tree,
    // stepping into the current node if necessary
    public async Task Advance()
    {
        if (Status is WalkerStatusDone)
        {
            return;
        }

        var progress = (WalkerStatusProgress)Status;
        if (progress.Current is not MST)
        {
            await StepOver();
        }
        else
        {
            await StepInto();
        }
    }
}