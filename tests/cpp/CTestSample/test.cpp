#include <cassert>
#include <string>
#include <cstring>

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        return 1;
    }

    if (std::strcmp(argv[1], "addition") == 0)
    {
        assert(2 + 2 == 4);
        return 0;
    }

    if (std::strcmp(argv[1], "subtraction") == 0)
    {
        assert(2 - 2 == 0);
        return 0;
    }

    if (std::strcmp(argv[1], "string_compare") == 0)
    {
        assert(std::string("hello") == std::string("hello"));
        return 0;
    }

    return 1;
}
