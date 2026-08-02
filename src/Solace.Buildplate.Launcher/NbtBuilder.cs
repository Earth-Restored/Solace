using Cyotek.Data.Nbt;

namespace Solace.Buildplate.Launcher;

internal sealed class NbtBuilder
{
    internal sealed class Compound
    {
        private readonly List<Tag> tags = [];

        public Compound()
        {
            // empty
        }

        public TagCompound Build(string name)
        {
            var tag = new TagCompound(name);
            foreach (var item in tags)
            {
                tag.Value.Add(item);
            }

            return tag;
        }

        public Compound Add(string name, int value)
        {
            var tag = new TagInt(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, byte value)
        {
            var tag = new TagByte(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, short value)
        {
            var tag = new TagShort(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, long value)
        {
            var tag = new TagLong(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, float value)
        {
            var tag = new TagFloat(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, double value)
        {
            var tag = new TagDouble(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, string value)
        {
            var tag = new TagString(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, int[] value)
        {
            var tag = new TagIntArray(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, byte[] value)
        {
            var tag = new TagByteArray(name, value);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, long[] value)
            => throw new NotImplementedException();//LongArrayTag tag = new LongArrayTag(name);//tag.setValue(value);//tags.add(tag);//return this;

        public Compound Add(string name, Compound value)
        {
            TagCompound tag = value.Build(name);
            tags.Add(tag);
            return this;
        }

        public Compound Add(string name, List value)
        {
            TagList tag = value.Build(name);
            tags.Add(tag);
            return this;
        }
    }

    internal sealed class List
    {
        private readonly TagType type;
        private readonly List<Tag> tags = [];

        public List(TagType type)
        {
            this.type = type;
        }

        public TagList Build(string name)
        {
            var tag = new TagList(name, type);
            foreach (var item in tags)
            {
                tag.Value.Add(item);
            }

            return tag;
        }

        public List Add(int value)
        {
            var tag = new TagInt("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(byte value)
        {
            var tag = new TagByte("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(short value)
        {
            var tag = new TagShort("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(long value)
        {
            var tag = new TagLong("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(float value)
        {
            var tag = new TagFloat("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(double value)
        {
            var tag = new TagDouble("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(string value)
        {
            var tag = new TagString("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(int[] value)
        {
            var tag = new TagIntArray("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(byte[] value)
        {
            var tag = new TagByteArray("", value);
            tags.Add(tag);
            return this;
        }

        public List Add(long[] value)
            => throw new NotImplementedException();//LongArrayTag tag = new LongArrayTag("");//tag.setValue(value);//this.tags.add(tag);//return this;

        public List Add(Compound value)
        {
            TagCompound tag = value.Build("");
            tags.Add(tag);
            return this;
        }

        public List Add(List value)
        {
            TagList tag = value.Build("");
            tags.Add(tag);
            return this;
        }
    }
}