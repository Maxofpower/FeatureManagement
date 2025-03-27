
using System;
using System.Threading.Tasks;

public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
	private static readonly Unit _value = new();

	public static ref readonly Unit Value => ref _value;

	public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(_value);

	public int CompareTo(Unit other) => 0;

	int IComparable.CompareTo(object obj) => 0;

	
	public override int GetHashCode() => 0;

	public bool Equals(Unit other) => true;


	public override bool Equals(object obj) => obj is Unit;


	public static bool operator ==(Unit first, Unit second) => true;

	
	public static bool operator !=(Unit first, Unit second) => false;
	public override string ToString() => "()";
}