using System;

public enum BTStatus
{
    Failure,
    Success,
    Running
}

/// <summary>小型响应式行为树。高优先级分支可中止当前 Running 分支。</summary>
public abstract class BTNode
{
    public string Name { get; }
    public BTStatus Status { get; protected set; }

    protected BTNode(string name)
    {
        Name = name;
    }

    public abstract BTStatus Tick(float deltaTime);

    public virtual void Abort()
    {
        Status = BTStatus.Failure;
    }
}

public sealed class BTCondition : BTNode
{
    private readonly Func<bool> condition;

    public BTCondition(string name, Func<bool> condition) : base(name)
    {
        this.condition = condition;
    }

    public override BTStatus Tick(float deltaTime)
    {
        return Status = condition() ? BTStatus.Success : BTStatus.Failure;
    }
}

public sealed class BTAction : BTNode
{
    private readonly Func<float, BTStatus> action;
    private readonly Action abort;

    public BTAction(string name, Func<float, BTStatus> action, Action abort = null) : base(name)
    {
        this.action = action;
        this.abort = abort;
    }

    public override BTStatus Tick(float deltaTime)
    {
        return Status = action(deltaTime);
    }

    public override void Abort()
    {
        if (Status == BTStatus.Running)
        {
            abort?.Invoke();
        }

        base.Abort();
    }
}

public sealed class BTSequence : BTNode
{
    private readonly BTNode[] children;
    private int _runningChildIndex = -1;

    public BTSequence(string name, params BTNode[] children) : base(name)
    {
        this.children = children;
    }

    /// <summary>
    /// 执行子节点，直到遇到第一个失败或正在运行的节点。若所有子节点都成功，则返回成功。
    /// </summary>
    /// <param name="deltaTime"></param>
    /// <returns></returns>
    public override BTStatus Tick(float deltaTime)
    {
        for (int i = 0; i < children.Length; i++)
        {
            BTStatus status = children[i].Tick(deltaTime);
            if (status == BTStatus.Success)
            {
                continue;
            }

            if (_runningChildIndex >= 0 &&
                _runningChildIndex != i)
            {
                children[_runningChildIndex].Abort();
            }

            if (status == BTStatus.Running)
            {
                _runningChildIndex = i;
            }
            else
            {
                _runningChildIndex = -1;
            }

            return Status = status;
        }

        _runningChildIndex = -1;
        return Status = BTStatus.Success;
    }

    public override void Abort()
    {
        if (_runningChildIndex >= 0)
        {
            children[_runningChildIndex].Abort();
        }

        _runningChildIndex = -1;
        base.Abort();
    }
}

public sealed class BTSelector : BTNode
{
    private readonly BTNode[] children;
    private int _runningChildIndex = -1;

    public BTSelector(string name, params BTNode[] children) : base(name)
    {
        this.children = children;
    }

    /// <summary>
    /// 执行子节点，直到遇到第一个成功或正在运行的节点。若所有子节点都失败，则返回失败。
    /// 若当前正在运行的子节点被中止，则继续执行下一个子节点。
    /// </summary>
    /// <param name="deltaTime"></param>
    /// <returns></returns>
    public override BTStatus Tick(float deltaTime)
    {
        for (int i = 0; i < children.Length; i++)
        {
            BTStatus status = children[i].Tick(deltaTime);
            if (status == BTStatus.Failure)
            {
                continue;
            }

            if (_runningChildIndex >= 0 &&
                _runningChildIndex != i)
            {
                children[_runningChildIndex].Abort();
            }

            if (status == BTStatus.Running)
            {
                _runningChildIndex = i;
            }
            else
            {
                _runningChildIndex = -1;
            }

            return Status = status;
        }

        if (_runningChildIndex >= 0)
        {
            children[_runningChildIndex].Abort();
        }

        _runningChildIndex = -1;
        return Status = BTStatus.Failure;
    }

    public override void Abort()
    {
        if (_runningChildIndex >= 0)
        {
            children[_runningChildIndex].Abort();
        }

        _runningChildIndex = -1;
        base.Abort();
    }
}
