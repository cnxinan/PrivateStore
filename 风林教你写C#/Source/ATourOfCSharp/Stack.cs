using System;

namespace Acme.Collections
{
	public class Stack
	{
		Entry top;
		public void Push(Object data)
		{
			top = new Entry(top, data);
		}

		public Object Pop()
		{
			if(top == null)
			{
				throw new InvalidOperationException();
			}
			Object data = top.data;
			top = top.next;
			return data;
		}

		class Entry
		{
			public Entry next;
			public Object data;
			public Entry(Entry next,Object data)
			{
				this.next = next;
				this.data = data;
			}
		}
	}
}