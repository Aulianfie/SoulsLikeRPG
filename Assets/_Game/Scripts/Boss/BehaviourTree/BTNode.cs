using System;

public enum BTStatus { Failure, Success, Running }

/// <summary>小型响应式行为树。高优先级分支可中止当前 Running 分支。</summary>
public abstract class BTNode
{
    public string Name { get; }
    public BTStatus Status { get; protected set; }
    protected BTNode(string name) { Name = name; }
    public abstract BTStatus Tick(float deltaTime);
    public virtual void Abort() { Status = BTStatus.Failure; }
}

public sealed class BTCondition : BTNode
{
    readonly Func<bool> condition;
    public BTCondition(string name, Func<bool> condition) : base(name) { this.condition = condition; }
    public override BTStatus Tick(float deltaTime) => Status = condition() ? BTStatus.Success : BTStatus.Failure;
}

public sealed class BTAction : BTNode
{
    readonly Func<float, BTStatus> action;
    readonly Action abort;
    public BTAction(string name, Func<float, BTStatus> action, Action abort = null) : base(name) { this.action = action; this.abort = abort; }
    public override BTStatus Tick(float deltaTime) => Status = action(deltaTime);
    public override void Abort() { if (Status == BTStatus.Running) abort?.Invoke(); base.Abort(); }
}

public sealed class BTSequence : BTNode
{
    readonly BTNode[] children;
    int running = -1;
    public BTSequence(string name, params BTNode[] children) : base(name) { this.children = children; }
    public override BTStatus Tick(float deltaTime)
    {
        for (int i = 0; i < children.Length; i++)
        {
            BTStatus status = children[i].Tick(deltaTime);
            if (status == BTStatus.Success) continue;
            if (running >= 0 && running != i) children[running].Abort();
            running = status == BTStatus.Running ? i : -1;
            return Status = status;
        }
        running = -1;
        return Status = BTStatus.Success;
    }
    public override void Abort() { if (running >= 0) children[running].Abort(); running = -1; base.Abort(); }
}

public sealed class BTSelector : BTNode
{
    readonly BTNode[] children;
    int running = -1;
    public BTSelector(string name, params BTNode[] children) : base(name) { this.children = children; }
    public override BTStatus Tick(float deltaTime)
    {
        for (int i = 0; i < children.Length; i++)
        {
            BTStatus status = children[i].Tick(deltaTime);
            if (status == BTStatus.Failure) continue;
            if (running >= 0 && running != i) children[running].Abort();
            running = status == BTStatus.Running ? i : -1;
            return Status = status;
        }
        if (running >= 0) children[running].Abort();
        running = -1;
        return Status = BTStatus.Failure;
    }
    public override void Abort() { if (running >= 0) children[running].Abort(); running = -1; base.Abort(); }
}
